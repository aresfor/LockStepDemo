using System.Collections.Generic;
using System.IO;
using Lockstep.Logic;
using Lockstep.Serialization;
using Lockstep.Util;


//@TODO: record  refracture
public class RecordHelper
{
    private const int RECODER_FILE_VERSION = 0;

    public static void Serialize(string recordFilePath, Client client)
    {
        // var writer = new Serializer();
        // writer.Write(RECODER_FILE_VERSION);
        // //writer.Write(client.PlayerServerInfos.Length);
        // //@TODO: localPlayerId
        // //writer.Write(0);
        // writer.Write(client.PlayerServerInfos);
        //
        // var count = client.Frames.Count;
        // writer.Write(count);
        // for (int i = 0; i < count; i++)
        // {
        //     client.Frames[i].Serialize(writer);
        // }
        //
        // var bytes = writer.CopyData();
        //
        // var relPath = PathUtil.GetUnityPath(recordFilePath);
        // var dir = Path.GetDirectoryName(relPath);
        // if (!Directory.Exists(dir))
        // {
        //     Directory.CreateDirectory(dir);
        // }
        //
        // File.WriteAllBytes(relPath, bytes);
    }

    public static void Deserialize(string recordFilePath, Client client)
    {
#if !UNITY_EDITOR
        return;
#endif
        // var relPath = PathUtil.GetUnityPath(recordFilePath);
        // var bytes = File.ReadAllBytes(relPath);
        // var reader = new Deserializer(bytes);
        // var recoderFileVersion = reader.ReadInt32();
        // //client.playerCount = reader.ReadInt32();
        //
        // //@TODO: localPlayerId
        // //client.localPlayerId = reader.ReadInt32();
        // client.PlayerServerInfos = reader.ReadArray(client.PlayerServerInfos);
        //
        // var count = reader.ReadInt32();
        // client.Frames = new List<FrameInput>();
        // for (int i = 0; i < count; i++)
        // {
        //     var frame = new FrameInput();
        //     frame.Deserialize(reader);
        //     frame.tick = i;
        //     client.Frames.Add(frame);
        // }
    }
}