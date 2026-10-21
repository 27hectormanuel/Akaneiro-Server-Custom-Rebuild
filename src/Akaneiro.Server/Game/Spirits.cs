using System;
using System.Collections.Generic;
using System.Linq;
using Akaneiro.Server.Data;
using Akaneiro.Server.Protocol;
using Microsoft.Extensions.Logging;

namespace Akaneiro.Server.Game
{
    /// <summary>
    /// Shimizu's spirit helpers (PETNPC). They are rented for an hour, a day or a week at the
    /// prices of PetPrice.Price; the client opens its spirit shop on RolePetListAck, buys with
    /// BuyPet and, once that succeeded, picks the spirit with choosePet. The spirit itself follows
    /// the player on the client side (Player.CallOnSpirit) until PetRentTimeEndNTF.
    /// The chosen spirit earns the experience its hunter earns from monsters and grows to the
    /// level of PetPrice.XPNeed; its bonus ("Increases Power 2%") counts in PlayerStats.
    /// </summary>
    public sealed class Spirits
    {
        // Rent buttons of UI_SpriteShop_DetailPanel: 3 = one hour, 7 = one day, 12 = one week.
        public const int RentHour = 3, RentDay = 7, RentWeek = 12;

        private readonly Session _s;

        public Spirits(Session session) => _s = session;

        private CharacterData Character => _s.Character;
        private long Now => new DateTimeOffset(_s.World.Clock.UtcNow, TimeSpan.Zero).ToUnixTimeSeconds();

        /// <summary>Spirits of one kind share the id without its last two digits (Inugami 10xx).</summary>
        private static int Kind(int petId) => petId / 100;

        public void SendList()
        {
            ExpireRentals();
            var list = new vectorSPetInfo();
            foreach (SpiritRental rental in Character.Spirits)
            {
                list.Add(new SPetInfo
                {
                    petTypeID = rental.PetId,
                    petLevel = _s.World.Data.Spirits.TryGetValue(rental.PetId, out var row) ? row.Level : 1,
                    petExp = Character.SpiritExp.GetValueOrDefault(Kind(rental.PetId)),
                    buypetBegTime = (ulong)rental.BeginUnix,
                    leftTime = rental.Seconds,
                });
            }
            _s.Send(GameResponses.RolePetListAck(list));
        }

        public void Rent(int petId, int rent)
        {
            int error = TryRent(petId, rent);
            if (error != EServerErrorType.eSuccess)
                _s.Log.LogInformation("   spirit {Pet} rejected ({Error})", petId, error);
            _s.Send(GameResponses.BuyPetAck(petId, rent, Proto.Error(error)));
        }

        private int TryRent(int petId, int rent)
        {
            if (!_s.World.Data.Spirits.TryGetValue(petId, out GameData.SpiritRow spirit))
                return EServerErrorType.eUnknownError;
            if (Character.Level < spirit.LevelRequired)
                return EServerErrorType.eGS_LessLevel;

            ((int Price, bool Crystal) cost, int seconds) = rent switch
            {
                RentHour => (spirit.Hour, 3600),
                RentDay => (spirit.Day, 86400),
                RentWeek => (spirit.Week, 7 * 86400),
                _ => ((0, false), 0),
            };
            if (seconds == 0)
                return EServerErrorType.eUnknownError;
            // Crystals are the real-money currency and cost nothing here; karma is paid.
            if (!cost.Crystal)
            {
                if (_s.Account.Karma < cost.Price)
                    return EServerErrorType.eGS_LessMoney_karma;
                _s.Account.Karma -= cost.Price;
                _s.SendMoney();
            }

            Character.Spirits.RemoveAll(r => Kind(r.PetId) == Kind(petId));
            Character.Spirits.Add(new SpiritRental { PetId = petId, BeginUnix = Now, Seconds = seconds });
            _s.SaveAccount();
            _s.Log.LogInformation("   rented {Spirit} for {Seconds} s", spirit.Name, seconds);
            return EServerErrorType.eSuccess;
        }

        public void Choose(int petId)
        {
            ExpireRentals();
            SpiritRental rental = Character.Spirits.FirstOrDefault(r => r.PetId == petId);
            if (rental == null)
            {
                _s.Send(GameResponses.choosePetAck(petId, Proto.Error(EServerErrorType.eUnknownError)));
                return;
            }
            foreach (SpiritRental other in Character.Spirits)
                other.Chosen = other == rental;
            _s.SaveAccount();
            _s.Send(GameResponses.choosePetAck(petId, Proto.Error(EServerErrorType.eSuccess)));
        }

        /// <summary>The spirit at the player's side, if any.</summary>
        public GameData.SpiritRow Active()
        {
            SpiritRental rental = Character?.Spirits.FirstOrDefault(r => r.Chosen && Now < r.BeginUnix + r.Seconds);
            return rental != null && _s.World.Data.Spirits.TryGetValue(rental.PetId, out var row) ? row : null;
        }

        /// <summary>
        /// The chosen spirit shares the hunter's experience from monsters; enough of it brings the next
        /// level (the rental moves to that level's id, e.g. Inugami 1001 -> 1002).
        /// </summary>
        public void GainExperience(ulong xp)
        {
            SpiritRental rental = Character?.Spirits.FirstOrDefault(r => r.Chosen && Now < r.BeginUnix + r.Seconds);
            if (rental == null || xp == 0)
                return;
            int kind = Kind(rental.PetId);
            ulong total = Character.SpiritExp[kind] = Character.SpiritExp.GetValueOrDefault(kind) + xp;

            GameData.SpiritRow reached = _s.World.Data.Spirits.Values
                .Where(s => Kind(s.Id) == kind && s.XpNeeded <= total)
                .OrderByDescending(s => s.Level)
                .FirstOrDefault();
            if (reached != null && reached.Id != rental.PetId && reached.Level > (_s.World.Data.Spirits.GetValueOrDefault(rental.PetId)?.Level ?? 0))
            {
                rental.PetId = reached.Id;
                _s.Log.LogInformation("   spirit {Spirit} reached level {Level}", reached.Name, reached.Level);
                _s.Battle.SendAttributes();
            }
        }

        /// <summary>Sends the spirit home when its time is up; checked on requests and by the session tick.</summary>
        public void ExpireRentals()
        {
            if (Character == null)
                return;
            foreach (SpiritRental rental in Character.Spirits.Where(r => Now >= r.BeginUnix + r.Seconds).ToList())
            {
                Character.Spirits.Remove(rental);
                _s.SaveAccount();
                if (rental.Chosen)
                    _s.Send(GameResponses.PetRentTimeEndNTF(rental.PetId));
            }
        }
    }
}
