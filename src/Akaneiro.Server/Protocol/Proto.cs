using System;
using System.Collections.Generic;
using System.Reflection;

namespace Akaneiro.Server.Protocol
{
    /// <summary>
    /// Helpers for the generated protocol classes. Many of their reference-type fields are left null by
    /// their constructors, which makes the generated Write() methods throw. <see cref="New{T}"/> creates an
    /// instance with every nested field populated with an empty default.
    /// </summary>
    public static class Proto
    {
        public static T New<T>() where T : new()
        {
            var obj = new T();
            Fill(obj, 0);
            return obj;
        }

        /// <summary>Populates null fields of an existing object (recursively).</summary>
        public static T Fill<T>(T obj)
        {
            Fill(obj, 0);
            return obj;
        }

        private static void Fill(object obj, int depth)
        {
            if (obj == null || depth > 16)
                return;

            foreach (FieldInfo field in obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Type t = field.FieldType;
                if (t.IsValueType)
                    continue;

                object value = field.GetValue(obj);
                if (value == null)
                {
                    if (t == typeof(string))
                    {
                        field.SetValue(obj, "");
                        continue;
                    }
                    if (t.GetConstructor(Type.EmptyTypes) == null)
                        continue;
                    value = Activator.CreateInstance(t);
                    field.SetValue(obj, value);
                }
                Fill(value, depth + 1);
            }
        }

        public static SAttributeChange Attr(int type, int value)
        {
            return new SAttributeChange { attributeType = new EAttributeType(type), value = value };
        }

        public static vectorAttrChange Attrs(IEnumerable<KeyValuePair<int, int>> values)
        {
            var vec = new vectorAttrChange();
            foreach (var kv in values)
                vec.Add(Attr(kv.Key, kv.Value));
            return vec;
        }

        public static EServerErrorType Error(int code) => new EServerErrorType(code);
    }
}
