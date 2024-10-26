# .NET Framework to DotNet Core

This is an experiment on the practical use and workflow of converting a .NET Framework application to a DotNet Core Web API and React application.  The objective is:

- Use and improve Aici for code generation and conversion
- Convert code from one framework to another like for like
- For this purpose, we will also attempt to convert languages
- Prove the ability to convert .NET Framework Web Forms to DotNet Core API with a React front end.

The process is as follows:

1. **Discovery**
   1. **Build & Run Unmodified** - Start by building and running the unmodified code.
   2. **Code Analysis** - We need to determine the files with the least depenendcies.
   2. **Conversion Tracking** - We will want to track the files that were converted - from and to.

## 1. Discovery

The application selected for this project was BlogEngine due to it's use of Web Forms and .NET Framework.

- <https://github.com/BlogEngine/BlogEngine.NET>

### 1.1 Build and Run Unmodified

My primary environment is a Mac - and we want the new BlogEngine to be platform independent.  But when converting the application, we need to ensure that the code we have is functional, and we will need a functioning system to reference and debug as we convert it.

#### 1.1.1 Development Environment

We'll start by setting up the following development environment:

- Windows 2022 VM in AWS
- Visual Studio 2022 - Current state
- Visual Studio Code - Target State
- GIT
- Tortious GIT
- NotePad++
- PodMan

#### 1.1.2 Building Current State

When we open the project in Visual Sudio 2022, .NET Framework 4.5 is not avaiable.  We'll want to upgrade the projects to .NET Framework 4.8.

Review the application's documentation:

- BlogEngine.NET\README.md

First sanity check is to right click the solution in Visual Studio and build the solution.  Make sure that no errors were encountered.  Secondly, run the application.

Once we are able to build and run the application, we have confirmed that we have everything needed.  Next step is to do some analisys to determine where to start from.

### 1.4 File Dependencies

For this process, we'll use NDepend:

- <https://www.ndepend.com/download>

Right click the zip file, and choose properties.  At the bottom of the properties, you might have a checkbox to 'unblock'.  Check this and click OK.  This will allow the executables inside the zip file to execute.  Otherwise, when you double click, nothing will happen.

When you get nDepend up and running, you'll want to use a query such as:

```
from t in Application.Types
where t.SourceDecls.Any()
select new { 
   t,
   t.FullName,
   t.SourceDecls.FirstOrDefault().SourceFile.FilePathString,
   t.SourceDecls.FirstOrDefault().SourceFile.NbILInstructions,
   t.SourceDecls.FirstOrDefault().SourceFile.NbLinesOfCode,
   t.NbTypesUsingMe,
   t.NbTypesUsed
}
```

We'll want to export this to XLS and then massage the data so we have a summary of usage and used by per file.