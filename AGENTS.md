# Agent Guide

- Only generating code that is specifically asked for. Use all up to date knowledge.
- All code must be compliant with the style guide (STYLE_GUIDE.md).
- When generating code, make a comment at the top
that specifies what program generated it, who generated it, and when it was generated.
- The unity project is being developed in version Unity 6.2 (6000.2.8f1)
- Use the new unity input system for all input
- Our gameplaycore scene houses all of the game managers and other core functionalities while the board scenes are asycnhronously loaded in. Certain features may need to be able to communicate across scenes.
