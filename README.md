# Fluorite

![Fluorite](Images/Fluorite.160.png)

`Fluorite` - Simplest and fully-customizable RPC standalone infrastructure.

[![Project Status: WIP – Initial development is in progress, but there has not yet been a stable, usable release suitable for the public.](https://www.repostatus.org/badges/latest/wip.svg)](https://www.repostatus.org/#wip)

## NuGet

|Package|main|Description|
|:--|:--|:--|
|Fluorite|[![NuGet Fluorite](https://img.shields.io/nuget/v/Fluorite.svg?style=flat)](https://www.nuget.org/packages/Fluorite)|Meta-package of provides statically proxy|
|Fluorite.Dynamic|[![NuGet Fluorite.Dynamic](https://img.shields.io/nuget/v/Fluorite.Dynamic.svg?style=flat)](https://www.nuget.org/packages/Fluorite.Dynamic)|Meta-package of provides auto-mated dynamically proxy|

|Package|main|Description|
|:--|:--|:--|
|Fluorite.Core|[![NuGet Fluorite.Core](https://img.shields.io/nuget/v/Fluorite.Core.svg?style=flat)](https://www.nuget.org/packages/Fluorite.Core)|Core engine|
|Fluorite.Serializer|[![NuGet Fluorite.Serializer](https://img.shields.io/nuget/v/Fluorite.Serializer.svg?style=flat)](https://www.nuget.org/packages/Fluorite.Serializer)|Default serializer implementation (Json)|
|Fluorite.Transport|[![NuGet Fluorite.Transport](https://img.shields.io/nuget/v/Fluorite.Transport.svg?style=flat)](https://www.nuget.org/packages/Fluorite.Transport)|Default transport implementation (WebSocket)|

-----

## What is this ?

An implementation for bi-directional asynchronously RPC controller.
Features:

* Fully asynchronously sending/receiving operation with `ValueTask` type.
* RPC form with custom proxy interface.
* Can attach your own custom serializer likely Json or another form by simple interface.
  * Json, Bson, XML, Message Pack, protobuf and etc...
* Can attach your own custom transport likely WebSocket or another protocol by simple interface.
  * WebSocket, TCP/UDP direct, Pipe, IPC and etc...

-----

## Sample

### Common interface definition

```csharp
// nuget install Fluorite.Core

// You can use custom types each arguments/return value, constraint depending used serializer.
// (Default serializer is Newtonsoft.Json)
public sealed class Item
{
    public int Id;
    public string Label;
    public int Price;
}

// Require inherits `IHost` and methods returns ValueTask<T>.
public interface IShop : IHost
{
    ValueTask<Item[]> GetItemsAsync(string category, int max);
    ValueTask<int> PurchaseAsync(params int[] itemIds);
}
```

### Client side

```csharp
// nuget install Fluorite.Dynamic

// Connect to server with default websocket/json transport.
// (Optional: You can register client-side expose object at last arguments)
var nest = await Nest.Factory.ConnectAsync("server.example.com", 4649, false);
try
{
    var shop = nest.GetPeer<IShop>();
    var items = await shop.GetItemsAsync("Fruit", 100);

    var total = await shop.PurchaseAsync(items[3].Id);
    Console.WriteLine(total);
}
finally
{
    await nest.ShutdownAsync();
}
```

### Server side

```csharp
// nuget install Fluorite.Dynamic

// The exposed interface implementer.
public sealed class Shop : IShop
{
    public async ValueTask<Item[]> GetItemsAsync(string category, int max)
    {
        // ...
    }

    public async ValueTask<int> PurchaseAsync(params int[] itemIds)
    {
        // ...
    }
}

// Start default websocket/json server with expose objects.
var nest = await Nest.Factory.StartServer(4649, false, new Shop());
try
{
    // ...
}
finally
{
    await nest.ShutdownAsync();
}
```

-----

## Automatic proxy generator

TODO:

-----

## Advanced topic

### Customize with your own serializer

TODO:

### Customize with your own transport

TODO:

### Customize with your own proxy factory

TODO:

-----

## License

Apache-v2
