# Contributing

## Branch and release flow

1. Create feature branches from `develop` and merge completed work back into `develop`.
2. Test the integrated `develop` branch.
3. After explicit approval, open a pull request from `develop` to `main`.
4. Merge the pull request only after its required checks pass.
5. Create `native-v*` and `vsix-v*` release tags from the resulting `main` commit.

Do not commit feature work directly to `main`, create a release tag from
`develop`, or publish release artifacts before the `develop` changes have been
approved and promoted through a pull request.

Both release workflows verify that their tagged commit is reachable from
`origin/main` before building or publishing an artifact.
