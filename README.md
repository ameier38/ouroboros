[![CircleCI](https://circleci.com/gh/ameier38/ouroboros/tree/develop.svg?style=svg)](https://circleci.com/gh/ameier38/ouroboros/tree/develop)

# Ouroboros
F# functions and types to help with building event sourced applications.

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

## Development

### Prerequisites:

- Docker: See [andrewcmeier.com/win-dev](https://andrewcmeier.com/win-dev#docker)
for information on how to install on Windows.
- kubectl: See [andrewcmeier.com/win-dev](https://andrewcmeier.com/win-dev#kubectl)
for information on how to install on Windows.

### Structure
```
src
├── Dog                     --> Example application
├── Ouroboros               --> Main library
├── Ouroboros.EventStore    --> Event Store integration
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

### Advanced Testing
First, spin up a local Kubernetes cluster. See instructions for 
creating a local Kubernetes cluster [here](https://andrewcmeier.com/win-dev#kubernetes).

Next, install Helm. See instructions for installing Helm 
on [their website](https://docs.helm.sh/using_helm/#quickstart).

Next, install Event Store.
```
> helm repo add incubator http://storage.googleapis.com/kubernetes-charts-incubator
> helm install incubator/eventstore
```

Next, install OpenFaas. You can find instructions 
[here](https://github.com/openfaas/faas-netes/tree/master/chart/openfaas).

Then, change the `image` value in the `stack.yml` file to your own Docker registry.
> If it is a private image you can use the [`create-registry-secret` script](scripts/create-registry-secret.sh)
to create a Docker registry secret.

At the root of this repository, run the following to build and push the test images.
```
> faas build
> faas push
```

In another shell, forward the port of the OpenFaas gateway to a local port.
```
> kubectl port-forward --namespace=openfaas svc/gateway 8080
```

Back in the original shell, deploy the test service.
```
> faas deploy
```

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
