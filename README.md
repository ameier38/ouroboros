[![CircleCI](https://circleci.com/gh/ameier38/ouroboros/tree/develop.svg?style=svg)](https://circleci.com/gh/ameier38/ouroboros/tree/develop)

# fsharp-event-sourcing
F# functions for building event sourced applications

## Usage
1) To use Ouroboros in your own project, first add it to your `paket.dependencies` file:
    ```
    source https://www.nuget.org/api/v2

    nuget Ouroboros
    ```
    > For an example `paket.dependencies` file see the [file in this repository](./paket.dependencies)

2) Next, add a reference to the dependency in your `paket.references` file:
    ```
    Ouroboros
    ```
    > For an example `paket.references` file see the [file in the Tests directory](./src/Tests/paket.references)

3) Then install the dependencies
    ```
    $ .paket/paket.exe install
    ```

4) See [Dog.Implementation.fs](./src/Tests/Dog.Implementation.fs) for an example on how to incorporate into your project.

## Resources
Below are a list of resources to get started with event sourcing.
- [Domain Modeling Made Functional](https://pragprog.com/book/swdddf/domain-modeling-made-functional)
- [Event Sourcing Basics](https://eventstore.org/docs/event-sourcing-basics/index.html)
- [F# DDD](http://gorodinski.com/blog/2013/02/17/domain-driven-design-with-fsharp-and-eventstore/)
- [Inventory Item example](https://github.com/eulerfx/DDDInventoryItemFSharp)
- [FsUno](https://github.com/thinkbeforecoding/FsUno/blob/master/FsUno/Game.fs)
- [Greg Young video on DDD](https://youtu.be/LDW0QWie21s)
- [12 Things You Should Know About Event Sourcing](http://blog.leifbattermann.de/2017/04/21/12-things-you-should-know-about-event-sourcing/)
