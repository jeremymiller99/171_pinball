# Style Guide

## Code Style

- Based largely on C# at Google style guide: <https://google.github.io/styleguide/csharp-style.html>.
- It is not exactly the same, though. For all unknowns, refer there.

1. Indentation, spacing, and brace styling:
    - Use 4 spaces in order to indent code.
    - Use 'Allman' bracing style, using a line break before the opening brace.
    - Write only one statement per line.
    - Use a space after commas and before if/else/for/while etc. statements.
    - All functions/loops should have an additional blank line before another statement.
    - There should also be an additional blank line before the first function in a class.
    - Use a space before and after any boolean operators (<=, ==, >).
    - Use a space before and after any mathmatical operators (+, -, *)

2. Naming conventions:
    - Variable names should be descriptive and consise
    - Use camelCase for variable names. (even constants)
    - Use PascalCase for class and function names.
    - Names of interfaces should start with the letter 'I' and then follow PascalCase, ex: IInterface.

3. General Readability:
    - Lines should use a maximum of 100 columns, including comments.
    - Code should be able to speak for itself. Only use comments when absolutely neccessary.
    - Serialize all variables.
    - No magic numbers except 0 or 1 for identification or inverse purposes (ex. 1 / x, y > 0).

## Project Wide Specifications

1. Language Standard Compliance:
    - Using Unity Editor version 6000.2.8f1
    - Using .NET Standard 2.1

2. File Types:
    - Use PascalCase for file names, except for markdown files, which should be named with ALL_CAPS.
    - All image files should be .png
    - All 3D model files should be .fbx
    - All sound files should be .bank files for FMOD integration.

## Art Style Guide

1. This is where we put a link to our art style guide.
