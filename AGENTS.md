# Agent Guide

- Only generating code that is specifically asked for. Use all up to date knowledge.
- All code must be compliant with the style guide (STYLE_GUIDE.md).
- When generating code, make a comment at the top
that specifies what program generated it, who generated it, and when it was generated.
- The unity project is being developed in version Unity 6.2 (6000.2.8f1)
- Use the new unity input system for all input
- Our gameplaycore scene houses all of the game managers and other core functionalities while the board scenes are asycnhronously loaded in. Certain features may need to be able to communicate across scenes.
- Version system: format is `0.MAJOR.MINOR` where the second digit is big updates and the third is small changes. After each change, bump the version in `CHANGELOG.md` and update the `m_text: version X.Y.Z` field in `Assets/Scenes/Core/MainMenu.unity`, then add a dated entry to `CHANGELOG.md` describing what changed. Each entry must include a `_Contributor: <name>_` line (ask the user whose name to attribute it to if unclear). Current version: 0.7.0.
