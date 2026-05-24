using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.8 Iter 1 — ISerializer 接口契约测试。
    /// Core 内无实现，本测试用 MockBytesSerializer 验证接口可用 + 测试模式建立。
    /// </summary>
    [TestFixture]
    public class SerializerTests
    {
        [Test]
        public void MockSerializer_IsSupported_AcceptsString()
        {
            var ser = new MockBytesSerializer();
            Assert.IsTrue(ser.IsSupported(typeof(string)));
            Assert.IsTrue(ser.IsSupported(typeof(int)));
        }

        [Test]
        public void MockSerializer_SerializeString_RoundTrip()
        {
            var ser = new MockBytesSerializer();
            byte[] data = ser.Serialize("hello");
            string back = ser.Deserialize<string>(data);
            Assert.AreEqual("hello", back);
        }

        [Test]
        public void MockSerializer_SerializeInt_RoundTrip()
        {
            var ser = new MockBytesSerializer();
            byte[] data = ser.Serialize(42);
            int back = ser.Deserialize<int>(data);
            Assert.AreEqual(42, back);
        }

        [Test]
        public void MockSerializer_NonGenericRoundTrip()
        {
            var ser = new MockBytesSerializer();
            byte[] data = ser.Serialize(typeof(string), "foo");
            object back = ser.Deserialize(typeof(string), data);
            Assert.AreEqual("foo", back);
        }

        [Test]
        public void MockSerializer_UnsupportedType_Throws()
        {
            var ser = new MockBytesSerializer();
            Assert.IsFalse(ser.IsSupported(typeof(Guid)));
            Assert.Throws<NotSupportedException>(() => ser.Serialize(Guid.NewGuid()));
        }

        [Test]
        public void MockSerializer_DeserializeNullData_Throws()
        {
            var ser = new MockBytesSerializer();
            Assert.Throws<ArgumentNullException>(() => ser.Deserialize<string>(null));
        }

        [Test]
        public void MockSerializer_RegisterAsModule_RetrievableFromHost()
        {
            var host = new ModuleHost();
            host.Register<ISerializer>(new MockBytesSerializer());
            host.Initialize();

            Assert.IsNotNull(host.Get<ISerializer>());
            host.Shutdown();
        }

        // ===== Mock 实现：仅 Tests 程序集，验证 ISerializer 接口契约 =====

        private sealed class MockBytesSerializer : ISerializer
        {
            private static readonly HashSet<Type> _supported = new HashSet<Type>
            {
                typeof(string), typeof(int), typeof(long), typeof(float), typeof(double), typeof(bool)
            };

            public int Priority => -600;
            public IReadOnlyList<Type> DependsOn => Array.Empty<Type>();
            public void OnInit(IModuleHost host) { }
            public void Shutdown() { }

            public bool IsSupported(Type type) => _supported.Contains(type);

            public byte[] Serialize<T>(T value) => Serialize(typeof(T), value);

            public byte[] Serialize(Type type, object value)
            {
                if (!IsSupported(type))
                    throw new NotSupportedException($"MockBytesSerializer 不支持类型 {type.Name}");
                return Encoding.UTF8.GetBytes(value?.ToString() ?? string.Empty);
            }

            public T Deserialize<T>(byte[] data) => (T)Deserialize(typeof(T), data);

            public object Deserialize(Type type, byte[] data)
            {
                if (data == null) throw new ArgumentNullException(nameof(data));
                if (!IsSupported(type))
                    throw new NotSupportedException($"MockBytesSerializer 不支持类型 {type.Name}");

                string s = Encoding.UTF8.GetString(data);
                if (type == typeof(string)) return s;
                if (type == typeof(int)) return int.Parse(s);
                if (type == typeof(long)) return long.Parse(s);
                if (type == typeof(float)) return float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
                if (type == typeof(double)) return double.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
                if (type == typeof(bool)) return bool.Parse(s);
                throw new InvalidOperationException($"unreachable type {type.Name}");
            }
        }
    }
}
