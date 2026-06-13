# Reference Frameworks

These repositories are local reference material for MyTryGetFramework design work. Keep the repository bodies out of this Git repo; only this location record is tracked.

Expected local layout:

```text
ReferenceFramework/
├─ AlicizaX/
├─ BigCat/
├─ DGame/
├─ MyFramework/
├─ TEngine/
└─ hsenl/
```

Sources:

| Local path | Source |
|---|---|
| `ReferenceFramework/AlicizaX/` | `https://github.com/AlicizaX` (multi-package UPM) |
| `ReferenceFramework/BigCat/` | `https://github.com/hl845740757/BigCat.git` |
| `ReferenceFramework/DGame/` | `https://github.com/AmaniDawn/DGame.git` |
| `ReferenceFramework/MyFramework/` | `https://github.com/ZHOURUIH/MyFramework.git` |
| `ReferenceFramework/TEngine/` | `https://gitee.com/game-for-all_0/TEngine.git` |
| `ReferenceFramework/hsenl/` | `https://gitee.com/dcze/hsenl.git` |

Setup on a new machine:

```powershell
git clone https://github.com/hl845740757/BigCat.git ReferenceFramework/BigCat
git clone https://gitee.com/game-for-all_0/TEngine.git ReferenceFramework/TEngine
git clone https://gitee.com/dcze/hsenl.git ReferenceFramework/hsenl
git clone https://github.com/AmaniDawn/DGame.git ReferenceFramework/DGame
git clone https://github.com/ZHOURUIH/MyFramework.git ReferenceFramework/MyFramework
```

Known consumers:

- `.scratch/framework-design/research.md`
- `.scratch/framework-design/candidates.md`
