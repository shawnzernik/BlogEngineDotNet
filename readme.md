# .NET Framework to DotNet Core

This is an experiment on the practical use and workflow of converting a .NET Framework application to a DotNet Core Web API and React application.  The objective is:

- Use and improve Aici for code generation and conversion
- Convert code from one framework to another like for like
- For this purpose, we will also attempt to convert languages
- Prove the ability to convert .NET Framework Web Forms to DotNet Core API with a React front end.

The process is as follows:

1. **Discovery**
   1. **Build & Run Unmodified** - Start by building and running the unmodified code.
   2. **Conversion Tracking** - We will want to track the files that were converted - from and to.

## 1. Discovery

The application selected for this project was BlogEngine due to it's use of Web Forms and .NET Framework.

- <https://github.com/BlogEngine/BlogEngine.NET>

### 1.1 Build and Run Unmodified

My primary environment is a Mac - and we want the new BlogEngine to be platform independent.  But when converting the application, we need to ensure that the code we have is functional, and we will need a functioning system to reference and debug as we convert it.

### 1.2 Conversion Tracking

We used a command 'cloc' to list and tack the files.

- <https://github.com/AlDanial/cloc>

The command r