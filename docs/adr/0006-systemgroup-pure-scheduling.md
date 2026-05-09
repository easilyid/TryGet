# Keep SystemGroup as a pure scheduling group

MyTryGetFramework V0.1 uses SystemGroup only to organize execution order and responsibility. It does not become a domain boundary or a module system, because that would add another layer of architecture before the core composition model has been validated.
