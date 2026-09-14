# Collision source architecture

The Collision workspace deliberately separates reusable source definitions from scene state:

```text
AvailableSources (many reusable definitions)
                 |
                 | deep-copy assignment
                 v
CollisionScene.AssignedSource (null or one scene-owned snapshot)
                 |
                 v
          source-owned collision rays
```

`SceneCollectionService.AssignSource` is the boundary for assignment. It keeps the source in
`AvailableSources` and asks the scene to clone it. Replacing an assignment therefore never removes
the previous reusable definition, and editing one scene cannot mutate the library or another scene.

A collision scene stores prisms and at most one `AssignedSource`. It has no manual-ray collection and
no generated/projected source collections. Generated sources produce rays during domain-scene
construction; transferred sources contribute their preserved exact rays. Collision is explicit: a
source replacement invalidates old results and refreshes rendering, but does not start a calculation.

The persistence DTO retains legacy multi-source/manual-ray fields only so older JSON can be read.
Loading migrates legacy sources into the reusable library, assigns at most the first source snapshot,
and intentionally ignores legacy manual rays. New saves write only `AssignedSource` for scene source
state, while `AvailableSources` is persisted independently.
