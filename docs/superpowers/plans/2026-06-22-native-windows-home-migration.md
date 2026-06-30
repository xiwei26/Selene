# Native Windows Home Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the WinUI template screen with a functional Selene home page that matches the Flutter/native-macOS home experience.

**Architecture:** Put all data models, API clients, local history loading, and `HomeViewModel` in `SeleneNative.Core` so they can be tested without launching WinUI. Keep `MainWindow.xaml` as a minimal root container and construct the current WinUI shell in `MainWindow.xaml.cs`; this avoids the Windows App SDK XAML compiler crash seen with richer template-based XAML.

**Tech Stack:** C#/.NET 8, WinUI 3, xUnit, `HttpClient`, `System.Text.Json`, `INotifyPropertyChanged`.

---

## File Structure

- `native-windows/src/SeleneNative.Core/Models/DoubanMovie.cs`: flexible JSON model for Douban recent-hot cards.
- `native-windows/src/SeleneNative.Core/Models/BangumiItem.cs`: flexible JSON model for Bangumi calendar cards.
- `native-windows/src/SeleneNative.Core/Models/PlayRecord.cs`: continue-watching model compatible with Selene play record JSON.
- `native-windows/src/SeleneNative.Core/Services/DoubanClient.cs`: fetch hot movies, TV shows, and shows from Douban-compatible endpoints.
- `native-windows/src/SeleneNative.Core/Services/BangumiClient.cs`: fetch today's Bangumi calendar.
- `native-windows/src/SeleneNative.Core/Services/PlayRecordStore.cs`: load local play records for continue-watching.
- `native-windows/src/SeleneNative.Core/ViewModels/HomeViewModel.cs`: aggregate all home sections and expose load/error state.
- `native-windows/src/SeleneNative/MainWindow.xaml`: minimal root grid for the WinUI window.
- `native-windows/src/SeleneNative/MainWindow.xaml.cs`: instantiate and load `HomeViewModel`, then construct the navigation shell, home sections, cards, and poster images in WinUI code.
- `native-windows/tests/SeleneNative.Tests/Home/*Tests.cs`: model, client, store, and ViewModel coverage.

## Tasks

### Task 1: Models

**Files:**
- Create: `native-windows/src/SeleneNative.Core/Models/DoubanMovie.cs`
- Create: `native-windows/src/SeleneNative.Core/Models/BangumiItem.cs`
- Create: `native-windows/src/SeleneNative.Core/Models/PlayRecord.cs`
- Test: `native-windows/tests/SeleneNative.Tests/Home/HomeModelTests.cs`

- [ ] Write failing tests for Douban, Bangumi, and play record JSON parsing.
- [ ] Run `dotnet test tests/SeleneNative.Tests/SeleneNative.Tests.csproj -c Release -p:Platform=x64 --filter HomeModelTests` and verify the missing-type failure.
- [ ] Implement the three model files with flexible JSON parsing helpers.
- [ ] Re-run the filtered tests and verify they pass.

### Task 2: Services

**Files:**
- Create: `native-windows/src/SeleneNative.Core/Services/DoubanClient.cs`
- Create: `native-windows/src/SeleneNative.Core/Services/BangumiClient.cs`
- Create: `native-windows/src/SeleneNative.Core/Services/PlayRecordStore.cs`
- Test: `native-windows/tests/SeleneNative.Tests/Home/HomeServiceTests.cs`

- [ ] Write failing tests using a fake `HttpMessageHandler`.
- [ ] Verify failures show missing clients/stores.
- [ ] Implement API clients and local JSON play record loading.
- [ ] Re-run filtered tests and verify they pass.

### Task 3: Home ViewModel

**Files:**
- Create: `native-windows/src/SeleneNative.Core/ViewModels/HomeViewModel.cs`
- Test: `native-windows/tests/SeleneNative.Tests/Home/HomeViewModelTests.cs`

- [ ] Write failing tests for successful aggregation and all-empty error state.
- [ ] Verify failures show missing `HomeViewModel`.
- [ ] Implement dependency-injected providers and observable properties.
- [ ] Re-run filtered tests and verify they pass.

### Task 4: WinUI Home

**Files:**
- Modify: `native-windows/src/SeleneNative/MainWindow.xaml`
- Modify: `native-windows/src/SeleneNative/MainWindow.xaml.cs`

- [ ] Replace the template text with the Selene navigation shell.
- [ ] Keep XAML minimal and construct the shell in code-behind.
- [ ] Load home sections from `HomeViewModel`.
- [ ] Add poster image loading and stable card layout in WinUI code.
- [ ] Build the WinUI project.

### Task 5: Verify Package

**Files:**
- Modify only if needed: `native-windows/build.ps1`

- [ ] Run `.\build.ps1` from `native-windows`.
- [ ] Launch `publish/win-x64/SeleneNative.exe`.
- [x] Verify the main window opens with Selene home sections instead of `Hello, WinUI 3!`.
