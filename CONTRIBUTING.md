# Contributing

## Branch and release flow

1. Create feature branches from `develop` and merge completed work back into `develop`.
2. Test the integrated `develop` branch.
3. After explicit approval, open a pull request from `develop` to `main`.
4. Merge the pull request only after its required checks pass.
5. The verified VSIX publishes to the Visual Studio Marketplace automatically
   from the resulting `main` build.
6. Create `native-v*` or `vsix-v*` tags from that `main` commit when a matching
   GitHub release artifact is required.

Do not commit feature work directly to `main`, create a release tag from
`develop`, or publish release artifacts before the `develop` changes have been
approved and promoted through a pull request.

Release workflows verify that deployable commits are reachable from
`origin/main`; Marketplace deployment is additionally restricted to a direct
`refs/heads/main` push.
