# ItemFavoriteFrameworkAPI

This is a typed, soft-dependency wrapper over Item Favorite Framework's `ItemFavoriteFramework.API`.
The full reference is [docs/API.md](../docs/API.md).

## Use it

1. Get it into your plugin in one of two ways:
   - **Copy** [`src/ItemFavorites.cs`](src/ItemFavorites.cs) into your project. It is one self-contained file.
   - **Bundle** `ItemFavoriteFrameworkAPI.dll` into your plugin with ILRepack.
2. Declare a soft dependency on the plugin class, so the framework loads first:

   ```csharp
   [BepInDependency("MidnightsFX.ItemFavoriteFramework", BepInDependency.DependencyFlags.SoftDependency)]
   ```

3. Call it:

   ```csharp
   using ItemFavoriteFrameworkAPI;

   if (ItemFavorites.IsProtected(item)) { continue; }
   ```

Every method is safe when the framework is not installed. Queries return `false`, `0` or an empty
list; changes return `false`. Use `ItemFavorites.IsLoaded()` when you need to know which case you are in.

Each endpoint is bound once to a typed delegate on first use, so a call costs no reflection. If the
installed framework is older and lacks an endpoint, that one method keeps returning its default. Probe
with `HasEndpoint(name)` or `GetApiVersion()`.

## Build

```bash
dotnet build ItemFavoriteFrameworkAPI/ItemFavoriteFrameworkAPI.csproj -c Release
```

The build finds the game and BepInEx through the repository's `Directory.Build.props`. The DLL has
version 0.0.0.0 on purpose, so consumers never need a rebuild when the framework's version changes.
