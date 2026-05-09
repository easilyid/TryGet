# Adopt World / Entity / Aspect as the core composition language

MyTryGetFramework V0.1 is centered on composition rather than inheritance or module-first design, so its core runtime language is **World / Entity / Aspect**. We choose **World** instead of Scene to avoid colliding with Unity scene loading, and **Aspect** instead of Component to avoid colliding with Unity components and traditional ECS component semantics while still expressing composable data and capability slices.
