// TestClient: a smoke test of a running server over the real network path, like the Steam client:
// web service login, TCP connection, packet framing, heartbeat, entering a scene.
// Game logic is covered by tests/Akaneiro.Server.Tests, which run in-process in seconds.
//
//   dotnet run --project tools/TestClient -- [httpPort]

using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;

int httpPort = args.Length > 0 ? int.Parse(args[0]) : 80;
string steamId = "7656119" + DateTime.UtcNow.ToString("HHmmssfff"); // a fresh account per run

// 1. Steam web service login (Steamworks.SendMsgToServerForSteamLogin)
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
var authResponse = await http.PostAsync($"http://127.0.0.1:{httpPort}/client/steam/user/authorize",
    new FormUrlEncodedContent(new Dictionary<string, string> { ["ticket"] = "00" }));
var success = JsonDocument.Parse(await authResponse.Content.ReadAsStringAsync()).RootElement.GetProperty("success");
string token = success.GetProperty("token").GetString();
string[] server = success.GetProperty("server").GetString().Split(':');
Check(token.Length > 0, $"web service login (game server {server[0]}:{server[1]})");

// 2. Game connection
using var tcp = new TcpClient();
await tcp.ConnectAsync(server[0], int.Parse(server[1]));
tcp.ReceiveTimeout = 5000; // a missing answer fails the run instead of hanging it
var stream = tcp.GetStream();
var recorded = new List<(string Name, object[] Args)>();
var gameReceiver = DispatchProxy.Create<ProtocolGame_ICallbackReceiver, Recorder>();
((Recorder)(object)gameReceiver).Calls = recorded;
var battleReceiver = DispatchProxy.Create<ProtocolBattle_ICallbackReceiver, Recorder>();
((Recorder)(object)battleReceiver).Calls = recorded;

void Send(Packet p)
{
    byte[] bytes = p.GetByteStream();
    stream.Write(bytes, 0, bytes.Length);
}

byte[] ReadExactly(int n)
{
    var buf = new byte[n];
    for (int read = 0; read < n;)
    {
        int r = stream.Read(buf, read, n - read);
        if (r == 0) throw new EndOfStreamException();
        read += r;
    }
    return buf;
}

// Reads packets until the expected answer arrives; everything is parsed by the client's own code.
object[] Expect(string name)
{
    while (true)
    {
        int index = recorded.FindIndex(c => c.Name == name);
        if (index >= 0)
        {
            var call = recorded[index];
            recorded.RemoveAt(index);
            return call.Args;
        }

        byte[] header;
        try { header = ReadExactly(6); }
        catch (IOException) { throw new TimeoutException($"expected {name}; received: {string.Join(", ", recorded.Select(c => c.Name).Distinct())}"); }
        byte[] body = ReadExactly(BitConverter.ToInt32(header, 0));
        if (BitConverter.ToInt16(header, 4) != 0)
            continue;
        if (!ProtocolGame_ReceiveCallback.Parse(gameReceiver, new Packet(body)) &&
            !ProtocolBattle_ReceiveCallback.Parse(battleReceiver, new Packet(body)))
            throw new Exception("the client could not parse a packet");
    }
}

// Heartbeat: a ping must come back as a pong carrying the same 4 bytes.
var ping = new byte[10];
BitConverter.GetBytes(4).CopyTo(ping, 0);
BitConverter.GetBytes((short)1).CopyTo(ping, 4);
BitConverter.GetBytes(12345).CopyTo(ping, 6);
stream.Write(ping);
var pong = ReadExactly(10);
Check(BitConverter.ToInt16(pong, 4) == 2 && BitConverter.ToInt32(pong, 6) == 12345, "heartbeat pong");

Send(ProtocolGame_SendRequest.UserLogin(steamId, new EUserType(EUserType.eUserType_Steam), new EClientType(EClientType.eClientType_Standalone_Win), "", token));
Check(((vectorCharacterInfoLogins)Expect("On_UserLoginOK")[2]).Count == 0, "login with a new account");

var create = new SCharacterCreationData { nickname = "Smoke" + DateTime.Now.Second, style = 4, avatarTraits = new mapIntInt(), sex = new ESex(ESex.eSex_Female) };
Send(ProtocolGame_SendRequest.UserCreateCharacter(create));
var created = (SCharacterInfoLogin)Expect("On_UserCreateCharacterOK")[0];
Send(ProtocolGame_SendRequest.UserSelectCharacter(created.ID));
Expect("On_UserSelectCharacterOK");
Check(((SBagGroup)Expect("On_SendBagInfo")[0]).equipbag.iteminfos.Count > 0, "character created and selected with gear");

Send(ProtocolGame_SendRequest.AcceptMission(1001, 5011));
Expect("On_AcceptMissionOk");
Send(ProtocolBattle_SendRequest.EnterScene("Hub_Village_Tutorial"));
Expect("On_EnterSceneOk");
Send(ProtocolBattle_SendRequest.MapLoadOK());
Check(((SCharacterStartInfo)Expect("On_CharacterStartInfo")[0]).objectID == 1, "tutorial entered");
// The tutorial's monsters wait for their triggers; its baskets and chests are there at once.
Check(Expect("On_BreakableActorEnter").Length > 0, "level objects announced");

Send(ProtocolGame_SendRequest.pingRequest());
Expect("On_pingRequestAck");

Console.WriteLine("SMOKE TEST PASSED");
return 0;

static void Check(bool ok, string what)
{
    Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
    if (!ok) Environment.Exit(1);
}

/// <summary>Implements the client callback interfaces and records every call.</summary>
public class Recorder : DispatchProxy
{
    public List<(string Name, object[] Args)> Calls;

    protected override object Invoke(MethodInfo targetMethod, object[] args)
    {
        Calls.Add((targetMethod.Name, args));
        return null;
    }
}
