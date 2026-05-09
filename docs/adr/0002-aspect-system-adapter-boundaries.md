# Split Aspect, System, and Adapter responsibilities

MyTryGetFramework V0.1 uses **Aspect** for Entity-owned state plus local behavior, **System** for cross-Entity scheduling rules, and **Adapter** for Unity or other external runtime integration. This keeps the framework close to ECS composition without forcing purely anemic components, while preventing Unity lifecycle and MonoBehaviour concerns from leaking into the core model.
