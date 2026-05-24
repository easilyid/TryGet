using System;
using System.Collections.Generic;
using NUnit.Framework;
using TryGet.Async;

namespace TryGet.Tests
{
    /// <summary>
    /// V0.8 Iter 4 — IAssetSource / MemoryAssetSource 测试（含 TGTask 异步集成）。
    /// </summary>
    [TestFixture]
    public class AssetSourceTests
    {
        private sealed class FakePrefab { public string Name; }
        private sealed class FakeTexture { public int W, H; }

        [Test]
        public void Memory_NewIsEmpty()
        {
            var src = new MemoryAssetSource();
            Assert.AreEqual(0, src.LoadedCount);
            Assert.IsFalse(src.IsLoaded("any"));
        }

        [Test]
        public void Memory_AddGet_RoundTrip()
        {
            var src = new MemoryAssetSource();
            var prefab = new FakePrefab { Name = "Player" };
            src.Add("Prefab/Player", prefab);

            Assert.IsTrue(src.IsLoaded("Prefab/Player"));
            Assert.AreSame(prefab, src.Get<FakePrefab>("Prefab/Player"));
        }

        [Test]
        public void Memory_Get_Missing_Throws()
        {
            var src = new MemoryAssetSource();
            Assert.Throws<AssetNotFoundException>(() => src.Get<FakePrefab>("missing"));
        }

        [Test]
        public void Memory_TryGet_Missing_ReturnsFalse()
        {
            var src = new MemoryAssetSource();
            Assert.IsFalse(src.TryGet<FakePrefab>("missing", out var a));
            Assert.IsNull(a);
        }

        [Test]
        public void Memory_TryGet_WrongType_ReturnsFalse()
        {
            var src = new MemoryAssetSource();
            src.Add("Prefab/Player", new FakePrefab());
            Assert.IsFalse(src.TryGet<FakeTexture>("Prefab/Player", out _));
        }

        [Test]
        public void Memory_LoadAsync_Existing_CompletesImmediately()
        {
            var src = new MemoryAssetSource();
            var prefab = new FakePrefab { Name = "Player" };
            src.Add("Prefab/Player", prefab);

            var task = src.LoadAsync<FakePrefab>("Prefab/Player");
            Assert.IsTrue(task.IsCompleted);
            Assert.AreSame(prefab, task.GetAwaiter().GetResult());
        }

        [Test]
        public void Memory_LoadAsync_Missing_TaskThrowsAssetNotFound()
        {
            var src = new MemoryAssetSource();
            var task = src.LoadAsync<FakePrefab>("missing");
            Assert.IsTrue(task.IsCompleted, "Memory 实现不论成败都立即完成");
            Assert.Throws<AssetNotFoundException>(() => task.GetAwaiter().GetResult());
        }

        [Test]
        public void Memory_LoadAsync_WrongType_TaskThrowsInvalidCast()
        {
            var src = new MemoryAssetSource();
            src.Add("Prefab/Player", new FakePrefab());
            var task = src.LoadAsync<FakeTexture>("Prefab/Player");
            Assert.Throws<InvalidCastException>(() => task.GetAwaiter().GetResult());
        }

        [Test]
        public void Memory_LoadAsync_EmptyPath_TaskThrowsArgument()
        {
            var src = new MemoryAssetSource();
            var task = src.LoadAsync<FakePrefab>("");
            Assert.Throws<ArgumentException>(() => task.GetAwaiter().GetResult());
        }

        [Test]
        public void Memory_Unload_RemovesPath()
        {
            var src = new MemoryAssetSource();
            src.Add("p", new FakePrefab());
            src.Unload("p");
            Assert.IsFalse(src.IsLoaded("p"));
        }

        [Test]
        public void Memory_LoadedPaths_Reflects()
        {
            var src = new MemoryAssetSource();
            src.Add("a", new FakePrefab());
            src.Add("b", new FakeTexture());

            var paths = new List<string>(src.LoadedPaths);
            paths.Sort();
            Assert.AreEqual(new[] { "a", "b" }, paths.ToArray());
        }

        [Test]
        public async TGTask Memory_LoadAsync_AwaitableInAsyncBody()
        {
            var src = new MemoryAssetSource();
            var prefab = new FakePrefab { Name = "Hero" };
            src.Add("hero", prefab);

            var loaded = await src.LoadAsync<FakePrefab>("hero");
            Assert.AreSame(prefab, loaded);
            Assert.AreEqual("Hero", loaded.Name);
        }

        [Test]
        public void Memory_ModuleHost_Integration()
        {
            var host = new ModuleHost();
            var src = new MemoryAssetSource();
            src.Add("p", new FakePrefab { Name = "x" });
            host.Register<IAssetSource>(src);
            host.Initialize();

            var task = host.Get<IAssetSource>().LoadAsync<FakePrefab>("p");
            Assert.AreEqual("x", task.GetAwaiter().GetResult().Name);

            host.Shutdown();
        }

        [Test]
        public void Memory_Add_NullArgs_Throw()
        {
            var src = new MemoryAssetSource();
            Assert.Throws<ArgumentException>(() => src.Add("", new FakePrefab()));
            Assert.Throws<ArgumentNullException>(() => src.Add<FakePrefab>("p", null));
        }
    }
}
