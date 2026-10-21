using System;
using System.Collections.Generic;
using System.Text;
using SceneExtract;

/// <summary>
/// Reads the mission definitions (_UI_CS_MapLevelItem) from a built player file such as the
/// Steam build's sharedassets0.assets. Player files carry no type trees, so the MonoBehaviours
/// are decoded with the field layout of the client's classes (_UI_CS_MapLevelItem,
/// _UI_CS_Branch, _UI_CS_Task, _UI_CS_RamusTask): every MonoBehaviour is tried, and only those
/// that decode exactly and look like a mission are kept. The Steam build's missions differ from
/// some prefabs in the client repository (e.g. 5121 and 5121_Old), so this is the source to trust.
/// </summary>
public static class BuiltMissions
{
    private const int MonoBehaviourClassId = 114;

    public static SortedDictionary<int, Dictionary<string, object>> Read(UnitySerializedFile file)
    {
        var missions = new SortedDictionary<int, Dictionary<string, object>>();
        foreach (UnitySerializedFile.ObjectInfo info in file.Objects.Values)
        {
            if (info.ClassId != MonoBehaviourClassId)
                continue;
            Dictionary<string, object> mission = TryMission(file.Raw(info.PathId));
            if (mission == null)
                continue;
            int id = (int)mission["ID"];
            if (missions.ContainsKey(id))
                Console.Error.WriteLine($"mission {id} appears twice (object {info.PathId}); keeping the first");
            else
                missions[id] = mission;
        }
        return missions;
    }

    private static Dictionary<string, object> TryMission(byte[] data)
    {
        try
        {
            var r = new LittleReader(data);
            // MonoBehaviour: m_GameObject, m_Enabled, m_Script, m_Name
            r.Int(); r.Int();
            r.Byte(); r.Align();
            r.Int(); r.Int();
            r.String();

            var m = new Dictionary<string, object>
            {
                ["name"] = r.String(),
                ["ID"] = r.Int(),
                ["ThreatLevel"] = r.Int(),
                ["levelMin"] = r.Int(),
                ["levelMax"] = r.Int(),
                ["NewMissionID"] = r.String(),
                ["PredecessorsMission"] = r.String(),
                ["MissionTime"] = r.Int(),
            };
            int id = (int)m["ID"];
            if (id < 5000 || id > 6999)
                return null;
            m["branchArray"] = r.List(Branch);
            m["xp"] = r.Int();
            m["sk"] = r.Int();
            m["mapName"] = r.String();
            m["mapID"] = r.Int();
            m["costBadge"] = r.Int();
            m["threatUp"] = r.Int();
            m["threatDown"] = r.Int();
            return r.AtEnd ? m : null;
        }
        catch (Exception ex) when (ex is ArgumentException || ex is IndexOutOfRangeException || ex is InvalidOperationException)
        {
            return null;
        }
    }

    private static Dictionary<string, object> Branch(LittleReader r)
    {
        var b = new Dictionary<string, object> { ["taskArray"] = r.List(Task) };
        b["BranchName"] = r.String();
        b["BranchID"] = r.Int();
        b["isBonus"] = (int)r.Byte();
        r.Align();
        b["rewardContentKarma"] = r.Int();
        b["rewardContentExp"] = r.Int();
        return b;
    }

    private static Dictionary<string, object> Task(LittleReader r)
    {
        var t = new Dictionary<string, object> { ["SubObject"] = r.List(Objective) };
        t["TaskTime"] = r.Int();
        t["triggerId"] = r.String();
        t["triggerType"] = r.String();
        t["rewardContentKarma"] = r.Int();
        t["rewardContentExp"] = r.Int();
        t["TaskID"] = r.Int();
        t["CurrentTimeVal"] = r.Float();
        t["CurrentPassimeVal"] = r.Float();
        t["StartTimeVal"] = r.Float();
        t["taskName"] = r.String();
        return t;
    }

    private static Dictionary<string, object> Objective(LittleReader r) => new()
    {
        ["typeID"] = r.Int(),
        ["objectID"] = r.Int(),
        ["count"] = r.Int(),
        ["recycle"] = r.Int(),
        ["name"] = r.String(),
        ["description"] = r.String(),
        ["ramusID"] = r.Int(),
        ["CurrentVal"] = r.Int(),
        ["CurrentValToString"] = r.String(),
    };

    private sealed class LittleReader
    {
        private readonly byte[] _d;
        private int _pos;

        public LittleReader(byte[] d) => _d = d;

        public bool AtEnd => _pos == _d.Length;

        public byte Byte() => _d[_pos++];

        public int Int()
        {
            int v = BitConverter.ToInt32(_d, _pos);
            _pos += 4;
            return v;
        }

        public float Float()
        {
            float v = BitConverter.ToSingle(_d, _pos);
            _pos += 4;
            return v;
        }

        public void Align() => _pos = (_pos + 3) & ~3;

        public string String()
        {
            int length = Int();
            if (length < 0 || length > 4096 || _pos + length > _d.Length)
                throw new InvalidOperationException("not a string");
            string s = Encoding.UTF8.GetString(_d, _pos, length);
            _pos += length;
            Align();
            return s;
        }

        public List<Dictionary<string, object>> List(Func<LittleReader, Dictionary<string, object>> element)
        {
            int count = Int();
            if (count < 0 || count > 256)
                throw new InvalidOperationException("not a list");
            var list = new List<Dictionary<string, object>>(count);
            for (int i = 0; i < count; i++)
                list.Add(element(this));
            return list;
        }
    }
}
