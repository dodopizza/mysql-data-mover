# Continuous Integration and Release Process

This project has adopted [GitHub Flow](https://guides.github.com/introduction/flow/index.html) for development lifecycle.

Also Continuous Integration (CI) and some routine actions are achieved using [GitHub Actions](https://github.com/features/actions).

## Workflows

There are several workflows to react on different GitHub events:

- [Continuous Integration](./on-push.yml)
  - _Purpose_: Build application docker image and run unit tests to ensure that changes doesn't broke anything.
  - _Run conditions_: Runs on every `push` event to any branch except `master`.

- [On Pull Request](./on-pull-request.yml)
  - _Purpose_: Build application docker image and run unit and integration tests to ensure that changes doesn't broke anything.
  - _Run conditions_: On every `pull request` to `master` branch.

- [On Push to Master](./on-push-to-master.yml)
  - _Purpose_: Build application docker image and run unit and integration tests on `master` branch and create draft for the release.
  - _Run conditions_: Runs on every `push` event to `master` branch.

- [On Release](./on-release.yml)
  - _Purpose_: Publish new release as Docker image into [Docker Hub](https://hub.docker.com/r/dodopizza/mysql-data-mover).
  - _Run conditions_: Runs on every `release published` event.

## How to publish new release

1. On every `push` event to `master` branch there is created draft for the future release (automated with `On Push To Master` workflow).
2. Double check application version in the `VersionPrefix` field in [Dodo.DataMover.csproj](src/Dodo.DataMover/Dodo.DataMover.csproj).
3. You have to check release notes in the release draft. It is good practices to describe all changes in the release and add links to the issues for each change.
4. Publish the release. `On Release` workflow will publish new release as Docker image into [Docker Hub](https://hub.docker.com/r/dodopizza/mysql-data-mover).
