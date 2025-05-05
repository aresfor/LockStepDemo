using Lockstep.Math;

namespace Lockstep.Serialization {
    public static class ExtensionSerializer {
        public static void Write(this Serializer serializer, LFloat val){
            serializer.Write(val._val);
        }

        public static void Write(this Serializer serializer, LVector2 val){
            serializer.Write(val._x);
            serializer.Write(val._y);
        }

        public static void Write(this Serializer serializer, LVector3 val){
            serializer.Write(val._x);
            serializer.Write(val._y);
            serializer.Write(val._z);
        }


        public static LFloat ReadLFloat(this Deserializer reader){
            var x = reader.ReadInt32();
            return new LFloat(true, x);
        }

        public static LVector2 ReadLVector2(this Deserializer reader){
            var x = reader.ReadInt32();
            var y = reader.ReadInt32();
            return new LVector2(true, x, y);
        }

        public static LVector3 ReadLVector3(this Deserializer reader){
            var x = reader.ReadInt32();
            var y = reader.ReadInt32();
            var z = reader.ReadInt32();
            return new LVector3(true, x, y, z);
        }
    }
}