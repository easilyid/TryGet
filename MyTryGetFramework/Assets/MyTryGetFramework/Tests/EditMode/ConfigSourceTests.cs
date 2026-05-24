using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.8 Iter 3 — IConfigSource / ConfigLoader<T> / MemoryConfigSource 测试。
    /// </summary>
    [TestFixture]
    public class ConfigSourceTests
    {
        // ===== MemoryConfigSource =====

        [Test]
        public void MemorySource_NewIsEmpty()
        {
            var src = new MemoryConfigSource();
            Assert.AreEqual(0, src.Count);
            Assert.IsFalse(src.Has("any"));
        }

        [Test]
        public void MemorySource_SetRawGetRaw_RoundTrip()
        {
            var src = new MemoryConfigSource();
            byte[] data = Encoding.UTF8.GetBytes("weapon-data");
            src.SetRaw("weapon.sword", data);

            Assert.IsTrue(src.Has("weapon.sword"));
            byte[] back = src.GetRaw("weapon.sword");
            Assert.AreEqual("weapon-data", Encoding.UTF8.GetString(back));
        }

        [Test]
        public void MemorySource_GetRaw_Missing_Throws()
        {
            var src = new MemoryConfigSource();
            Assert.Throws<ConfigNotFoundException>(() => src.GetRaw("missing"));
        }

        [Test]
        public void MemorySource_TryGetRaw_Missing_ReturnsFalse()
        {
            var src = new MemoryConfigSource();
            Assert.IsFalse(src.TryGetRaw("missing", out var data));
            Assert.IsNull(data);
        }

        [Test]
        public void MemorySource_SetRaw_NullData_Throws()
        {
            var src = new MemoryConfigSource();
            Assert.Throws<ArgumentNullException>(() => src.SetRaw("k", null));
        }

        [Test]
        public void MemorySource_SetRaw_OverwriteAllowed()
        {
            var src = new MemoryConfigSource();
            src.SetRaw("k", new byte[] { 1, 2 });
            src.SetRaw("k", new byte[] { 9, 8, 7 });
            Assert.AreEqual(3, src.GetRaw("k").Length);
        }

        [Test]
        public void MemorySource_ConfigIds_ReflectsSetCalls()
        {
            var src = new MemoryConfigSource();
            src.SetRaw("a", new byte[] { 1 });
            src.SetRaw("b", new byte[] { 2 });

            var ids = new List<string>(src.ConfigIds);
            ids.Sort();
            Assert.AreEqual(new[] { "a", "b" }, ids.ToArray());
        }

        [Test]
        public void MemorySource_Remove_Works()
        {
            var src = new MemoryConfigSource();
            src.SetRaw("k", new byte[] { 1 });
            Assert.IsTrue(src.Remove("k"));
            Assert.IsFalse(src.Has("k"));
        }

        // ===== ConfigLoader<T> =====

        private sealed class WeaponConfig { public int Atk; public string Name; }

        private static WeaponConfig DeserializeWeapon(byte[] data)
        {
            // 简单格式：UTF8 "Name|Atk"
            var s = Encoding.UTF8.GetString(data);
            var parts = s.Split('|');
            return new WeaponConfig { Name = parts[0], Atk = int.Parse(parts[1]) };
        }

        private static byte[] SerializeWeapon(WeaponConfig w) =>
            Encoding.UTF8.GetBytes($"{w.Name}|{w.Atk}");

        [Test]
        public void Loader_Get_DeserializesOnFirstAccess()
        {
            var src = new MemoryConfigSource();
            src.SetRaw("w1", SerializeWeapon(new WeaponConfig { Name = "sword", Atk = 100 }));

            int dezCount = 0;
            var loader = new ConfigLoader<WeaponConfig>(src, b => { dezCount++; return DeserializeWeapon(b); });

            var w = loader.Get("w1");
            Assert.AreEqual("sword", w.Name);
            Assert.AreEqual(100, w.Atk);
            Assert.AreEqual(1, dezCount);
        }

        [Test]
        public void Loader_Get_CachesAcrossCalls()
        {
            var src = new MemoryConfigSource();
            src.SetRaw("w1", SerializeWeapon(new WeaponConfig { Name = "sword", Atk = 100 }));

            int dezCount = 0;
            var loader = new ConfigLoader<WeaponConfig>(src, b => { dezCount++; return DeserializeWeapon(b); });

            var w1 = loader.Get("w1");
            var w2 = loader.Get("w1");
            var w3 = loader.Get("w1");

            Assert.AreEqual(1, dezCount, "缓存命中后不应重复反序列化");
            Assert.AreSame(w1, w2, "缓存应返回同一 instance");
            Assert.AreSame(w2, w3);
        }

        [Test]
        public void Loader_Get_MissingId_Throws()
        {
            var src = new MemoryConfigSource();
            var loader = new ConfigLoader<WeaponConfig>(src, DeserializeWeapon);
            Assert.Throws<ConfigNotFoundException>(() => loader.Get("missing"));
        }

        [Test]
        public void Loader_TryGet_Missing_ReturnsFalse()
        {
            var src = new MemoryConfigSource();
            var loader = new ConfigLoader<WeaponConfig>(src, DeserializeWeapon);
            Assert.IsFalse(loader.TryGet("missing", out var w));
            Assert.IsNull(w);
        }

        [Test]
        public void Loader_TryGet_DeserializerThrows_ReturnsFalse()
        {
            var src = new MemoryConfigSource();
            src.SetRaw("bad", new byte[] { 0xFF });
            var loader = new ConfigLoader<WeaponConfig>(src, _ => throw new FormatException("invalid"));

            Assert.IsFalse(loader.TryGet("bad", out var w));
            Assert.IsNull(w);
        }

        [Test]
        public void Loader_InvalidateCache_All_NextGetReDeserializes()
        {
            var src = new MemoryConfigSource();
            src.SetRaw("w1", SerializeWeapon(new WeaponConfig { Name = "v1", Atk = 1 }));

            int dezCount = 0;
            var loader = new ConfigLoader<WeaponConfig>(src, b => { dezCount++; return DeserializeWeapon(b); });

            loader.Get("w1");
            Assert.AreEqual(1, dezCount);

            // 模拟热重载：更新 source + invalidate cache
            src.SetRaw("w1", SerializeWeapon(new WeaponConfig { Name = "v2", Atk = 999 }));
            loader.InvalidateCache();

            var w = loader.Get("w1");
            Assert.AreEqual("v2", w.Name);
            Assert.AreEqual(999, w.Atk);
            Assert.AreEqual(2, dezCount, "InvalidateCache 后应重新反序列化");
        }

        [Test]
        public void Loader_InvalidateCache_SingleId_OnlyAffectsThat()
        {
            var src = new MemoryConfigSource();
            src.SetRaw("a", SerializeWeapon(new WeaponConfig { Name = "a", Atk = 1 }));
            src.SetRaw("b", SerializeWeapon(new WeaponConfig { Name = "b", Atk = 2 }));

            int dezCount = 0;
            var loader = new ConfigLoader<WeaponConfig>(src, raw => { dezCount++; return DeserializeWeapon(raw); });

            loader.Get("a");
            loader.Get("b");
            Assert.AreEqual(2, dezCount);
            Assert.AreEqual(2, loader.CachedCount);

            loader.InvalidateCache("a");
            Assert.AreEqual(1, loader.CachedCount);

            loader.Get("b"); // 应仍在缓存
            Assert.AreEqual(2, dezCount, "b 缓存未被 invalidate");

            loader.Get("a"); // 应重新反序列化
            Assert.AreEqual(3, dezCount);
        }

        [Test]
        public void Loader_NullArgs_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigLoader<WeaponConfig>(null, DeserializeWeapon));
            Assert.Throws<ArgumentNullException>(() => new ConfigLoader<WeaponConfig>(new MemoryConfigSource(), null));
        }

        [Test]
        public void Loader_ModuleHost_Integration()
        {
            var host = new ModuleHost();
            var src = new MemoryConfigSource();
            src.SetRaw("w1", SerializeWeapon(new WeaponConfig { Name = "sword", Atk = 100 }));
            host.Register<IConfigSource>(src);
            host.Initialize();

            var loader = new ConfigLoader<WeaponConfig>(host.Get<IConfigSource>(), DeserializeWeapon);
            Assert.AreEqual(100, loader.Get("w1").Atk);

            host.Shutdown();
        }
    }
}
