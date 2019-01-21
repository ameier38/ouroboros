# Ouroboros
![nuget version](https://img.shields.io/nuget/v/Ouroboros.svg)
![downloads](https://img.shields.io/nuget/dt/Ouroboros.svg)
___
__Ouroboros__ is a set of types and functions for building 
event sourced applications in F#. Ouroboros aims to be simple 
and flexible and best used for modeling bi-temporal domains
with complex state transitions.

> For applications with a large number of events checkout out
[Jet.com's Equinox](https://github.com/jet/equinox).

## Usage
1) To use Ouroboros in your own project, first add it to your `paket.dependencies` file:
    ```
    source https://www.nuget.org/api/v2

    nuget Ouroboros
    ```
    > For an example `paket.dependencies` file see the 
    [example in this repository](./paket.dependencies)

2) Next, add a reference to the dependency in your `paket.references` file:
    ```
    Ouroboros
    ```
    > For an example `paket.references` file see the 
    [example in this repository](./src/Dog/paket.references)

3) Then install the dependencies
    ```
    $ .paket/paket.exe install
    ```

4) See [the example application](./src/Dog/README.md) for a
complete project using Ouroboros.

## Development

### Prerequisites:

- `dotnet` CLI: See [andrewcmeier.com/win-dev](https://andrewcmeier.com/win-dev#dotnet)
for how to install on Windows.

### Structure
```
src
├── Dog                     --> Example application
├── Ouroboros               --> Main library
├── Ouroboros.EventStore    --> Event Store backend
└── Tests                   --> Test suite
```

### Testing
Start a local Event Store instance.
```
> docker-compose up -d eventstore
```

Navigate to the `Tests` directory and run the console application.
```
> cd src/Tests
> dotnet run
```

## Improvements
- [ ] Migrate to [SwaggerProvider](https://github.com/fsprojects/SwaggerProvider/pull/92)
- [ ] Add ability to reconnect if Event Store connection is dropped in API
- [ ] Add logging

## References
Below are a list of references used to create Ouroboros.
- [Inventory Item example](https://github.com/eulerfx/DDDInventoryItemFSharp)
- [FsUno](https://github.com/thinkbeforecoding/FsUno/blob/master/FsUno/Game.fs)
- [Pre and Post dated events](http://codebetter.com/gregyoung/2014/03/02/event-sourcing-and-postpre-dated-transactions/)
- [Event Store on Kubernetes on Google Cloud](https://blog.2mas.xyz/setting-up-event-store-with-kubernetes-on-google-cloud/)
- [Event Store on Kubernetes on AWS](http://www.dinuzzo.co.uk/2018/08/13/set-up-an-eventstore-cluster-on-kubernetes/)

## DDD-CQRS-ES Resources
Below are a list of resources to get started with Domain Driven Design (DDD),
Command Query Responsibility Segregation (CQRS), and Event Sourcing (ES).
- [Domain Modeling Made Functional](https://pragprog.com/book/swdddf/domain-modeling-made-functional)
- [Event Sourcing Basics](https://eventstore.org/docs/event-sourcing-basics/index.html)
- [F# DDD](http://gorodinski.com/blog/2013/02/17/domain-driven-design-with-fsharp-and-eventstore/)
- [Greg Young video on DDD](https://youtu.be/LDW0QWie21s)
- [12 Things You Should Know About Event Sourcing](http://blog.leifbattermann.de/2017/04/21/12-things-you-should-know-about-event-sourcing/)

## Similar Projects
- [Jet.com Equinox](https://github.com/jet/equinox)

<details>
    <summary>Versions</summary>

### 2.0.0
Added functionality to 'delete' an event from a stream
which effectively ignores these events when loaded from
the repository and, therefore, we do not apply them when
reconstituting the state. The reason for this is because
in the real world we may accidentally run commands
which produce valid events, but were genuine mistakes.
Having a 'delete event' command which records the deletion
but allows us to undo a command is easier to correct these errors.

### 1.0.0
Added boilerplate functions and type to work with event sourced
systems in F#. Added EventStore store.

</details>
