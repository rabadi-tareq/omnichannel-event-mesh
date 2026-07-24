---
name: solution-code-tree-diagram
description: Produce a code-aware tree diagram of a repository or .NET solution that shows projects, nested folders, code files only, and top-level types declared in each code file.
---

# Solution Code Tree Diagram

Use this skill when the user asks for a tree-style view of a solution or repository that includes only code files and their classes, records, interfaces, structs, enums, or top-level statements.

## Goal
Create a readable ASCII tree that shows:
- the solution root
- each project
- nested folders inside each project
- code files only
- top-level types declared in each code file

## Inclusion Rules
Include:
- project files as project nodes
- folders that contain code files or nested code folders
- code files such as `.cs`, `.csx`, and other source files in the repository’s language set
- top-level types declared in each code file
- partial classes and generated designer files when they are code files

## Exclusion Rules
Exclude:
- non-code files such as `.json`, `.http`, `.md`, `.xml`, `.yaml`, `.yml`, `.txt`
- build output folders such as `bin` and `obj`
- package restore files and other generated artifacts unless the user explicitly asks for them
- files that do not contribute source code structure

## Output Format
Use a plain-text tree with indentation, for example:

```text
SolutionName
└─ src
   ├─ ProjectA
   │  ├─ FolderOne
   │  │  └─ FileA.cs
   │  │     ├─ ClassA
   │  │     └─ ClassB
   │  └─ FileB.cs
   │     └─ top-level statements
   └─ ProjectB
	  └─ ...
```

## Type Reporting Rules
For each code file:
- list only top-level declared types
- show the type names beneath the file
- if the file has top-level statements instead of types, label it as `top-level statements`
- if a file contains a partial type, show the type name once and note it as partial only when helpful

## Style Rules
- Keep the diagram compact and readable
- Do not use Mermaid unless the user asks for Mermaid specifically
- Do not include a project-only summary unless the user asks for a simplified view
- Prefer the exact folder structure from the workspace
- When the user asks for “code files only,” do not include configuration, documentation, or test data files

## Clarification Rule
If the scope is ambiguous, ask whether the user wants:
- project tree only
- folder tree with code files
- folder tree with code files and top-level types
- Mermaid diagram

