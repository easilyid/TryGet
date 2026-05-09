# Reference Frameworks

These repositories are local reference material for MyTryGetFramework design work. Keep the repository bodies out of this Git repo; only this location record is tracked.

Expected local layout:

```text
ReferenceFramework/
├─ BigCat/
├─ TEngine/
└─ hsenl/
```

Sources:

| Local path | Source |
|---|---|
| `ReferenceFramework/BigCat/` | `https://github.com/hl845740757/BigCat.git` |
| `ReferenceFramework/TEngine/` | `https://gitee.com/game-for-all_0/TEngine.git` |
| `ReferenceFramework/hsenl/` | `https://gitee.com/dcze/hsenl.git` |

Setup on a new machine:

```powershell
git clone https://github.com/hl845740757/BigCat.git ReferenceFramework/BigCat
git clone https://gitee.com/game-for-all_0/TEngine.git ReferenceFramework/TEngine
git clone https://gitee.com/dcze/hsenl.git ReferenceFramework/hsenl
```

Known consumers:

- `.scratch/framework-design/research.md`
- `.scratch/framework-design/candidates.md`
