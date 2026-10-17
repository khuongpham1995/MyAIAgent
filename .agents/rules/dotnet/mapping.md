---
alwaysApply: true
description: >
  Mapper conventions for MyAIAgent. Covers placement, naming, method signatures,
  and what to map at each layer boundary.
---

# Mapping Rules

Keep application contract mappings in **`Application/Common/Mappings/`** as Mapster
`IRegister` classes. DTOs and entities stay free of mapping interfaces. Register
one application-owned `TypeAdapterConfig` per service, scan the Application
assembly, compile the mappings at startup, and resolve `MapsterMapper.IMapper`
through DI.

- Map inbound DTOs to commands at the Presentation boundary using the injected mapper.
- Map entities to response models in handlers using the injected mapper when needed.
- Keep mapping expressions pure. Business decisions and I/O belong in handlers or services.
- Declare a mapping for each application-contract pair, even when members share names.
- Pass authenticated identity through a trusted application source type; never map it from a client-controlled DTO field.

Do not use parameterless `Adapt<T>()` or static mapping extensions in application
code: they can bypass the DI-owned configuration. Do not put mapping rules in
Presentation, Infrastructure, Domain, or the DTOs themselves.
