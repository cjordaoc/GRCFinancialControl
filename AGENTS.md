# GRC Financial Control - Agent Operating Instructions

This document provides technical guidelines for agents working on this project.

## 1. Technology Stack

-   **Backend:** C# with .NET 8
-   **UI Framework:** Avalonia (MVVM)
-   **Data Persistence:** Entity Framework Core
-   **Database:** SQLite (for initial development), with potential for MySQL in production.
-   **Excel Data Reading:** A suitable library like EPPlus or NPOI.

## 2. Coding Principles

-   **MVVM Architecture:** Strictly adhere to the Model-View-ViewModel pattern for the Avalonia project.
    -   **Models:** Plain C# objects representing the core business domain (`Engagement`, `FiscalYear`). Reside in the `.Core` project.
    -   **Views:** XAML files defining the UI. Should contain minimal to no code-behind.
    -   **ViewModels:** C# classes that expose data from the models to the views and handle user interactions via commands.
-   **Dependency Injection:** Use dependency injection to decouple components, especially for services like data access or file parsing.
-   **Single Responsibility Principle:** Each class should have a single, well-defined purpose.
-   **Immutability:** Use immutable or read-only structures where possible to prevent unintended side effects.

## 3. Project Setup & Preflight Checks

To set up the development environment and verify its integrity:

1.  **Install .NET 8 SDK.**
2.  **Clone the repository.**
3.  **Restore Dependencies:**
    ```bash
    dotnet restore
    ```
4.  **Build the Solution:**
    ```bash
    dotnet build
    ```
5.  **Run Tests:**
    ```bash
    dotnet test
    ```

A successful build and test run confirms the environment is ready.

## 4. Data Validation Steps (Automated & Manual)

-   **File Schema Validation:** Before processing, verify that uploaded Excel files contain the expected columns as defined in `README.md`.
-   **Totals Check:** After import, the sum of hours in the database must match the sum of hours in the source file. This should be an automated check.
-   **Allocation Check:** The "Fiscal Year Allocation" screen must enforce that the sum of allocated hours equals the total planned hours for an engagement.
-   **Manual Verification:** Before submitting a change related to data processing, manually test the end-to-end user journey with the provided sample files in the `DataTemplate` directory.

## 5. Recovery Protocol (When Stuck)

If a development task is blocked by repeated, unresolvable errors:

1.  **Checkpoint:** Commit all validated, working code to a new branch.
    -   Name the branch `AutoCheckpoint_[feature]_[timestamp]`.
    -   Create a pull request with a clear description of what was completed and what the blocker is.
2.  **Restart:** Clean the solution (`dotnet clean`) and, if necessary, restart the development environment.
3.  **Resume:**
    -   Check out the latest version from the main branch.
    -   Merge the checkpoint branch if it contains valid work.
    -   Re-evaluate the approach to the blocked task.
4.  **Verify:** Run all preflight checks to ensure the environment is stable before continuing.

## 6. Quality Gates

-   **No Code-Behind:** Views should not contain business logic. All logic must be in ViewModels.
-   **Unit Tests:** All business logic (e.g., allocation validation, PAPD attribution) must be covered by unit tests.
-   **End-to-End Test:** The full user journey (Setup -> Upload -> Allocate -> Report) must be successfully tested before a major feature is considered complete.
-   **README Alignment:** All implemented features must align with the specifications in `README.md`. If a deviation is necessary, the `README.md` must be updated first.