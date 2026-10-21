CLIENT SOURCE FILES
===================

Unchanged copies of the files the server needs from the Akaneiro: Demon Hunters client source,
which Spicy Horse released into the public domain:
https://github.com/SpicyHorseLegacy/akaneiro-client (folder Assets/)

Scripts/CS/Comm, Scripts/CS/Protocol
    The network protocol. Compiled into the server and the test client, so both sides share one
    definition; ProtoGen derives the server side from it.
Resources/Localization/EN
    The game's data tables (items, abilities, levels, prices, ...), read when the server starts.

The paths match the client project's Assets folder. Do not edit these files; to update them, copy
them again from the client project.
