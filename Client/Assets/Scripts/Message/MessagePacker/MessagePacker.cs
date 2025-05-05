using System;
using System.Runtime.InteropServices;
using Lockstep.Network;
using Lockstep.Serialization;
using UnityEngine;

namespace Lockstep.Logic{
    public class MessagePacker : IMessagePacker {
        public static MessagePacker Instance { get; } = new MessagePacker();

        public object DeserializeFrom(ushort opcode, byte[] bytes, int index, int count){
            var type = (EMessageType) opcode;
            switch (type) {
                //case EMessageType.JoinRoom: return BaseFormater.FromBytes<Msg_JoinRoom>(bytes, index, count);
                //case EMessageType.QuitRoom: return BaseFormater.FromBytes<Msg_QuitRoom>(bytes, index, count);
                
                case EMessageType.PlayerInput: return BaseFormater.FromBytes<Msg_PlayerInput>(bytes, index, count);
                case EMessageType.FrameInput: return BaseFormater.FromBytes<Msg_FrameInput>(bytes, index, count);
                case EMessageType.StartGame: return BaseFormater.FromBytes<Msg_StartGame>(bytes, index, count);
                case EMessageType.PlayerHash: return BaseFormater.FromBytes<Msg_PlayerHash>(bytes, index, count);
            }

            return null;
        }

        public byte[] SerializeToByteArray(IMessage msg){
            return ((BaseFormater) msg).ToBytes();
        }


        public IntPtr GetBytesPtr(ushort opCode, byte[] data, out int totalLength)
        {
            unsafe
            {
                //opcode + size + bytes
                ushort size = (ushort)data.Length;
                totalLength = sizeof(ushort) + sizeof(ushort) + size;
                
                byte[] totalData = new byte[totalLength];
                Buffer.BlockCopy(BitConverter.GetBytes(opCode), 0, totalData,0, sizeof(ushort));
                Buffer.BlockCopy(BitConverter.GetBytes(size), 0, totalData,sizeof(ushort), sizeof(ushort));

                if (size > 0)
                {
                    Buffer.BlockCopy(data, 0, totalData, sizeof(ushort) * 2, size);
                }
                
                var newPtr = Marshal.AllocHGlobal(totalLength);
                Marshal.Copy(totalData, 0, newPtr, totalLength);
                //
                // fixed (byte* source = &data[0])
                // {
                //     Buffer.MemoryCopy(source, newPtr.ToPointer()
                //         , data.Length, data.Length);
                // }

                Debug.Log($"GetBytes:: opCode: {(EMessageType)opCode}, size: {size}, totalLength: {totalLength}");
                return newPtr;
            }
        }
    }
}