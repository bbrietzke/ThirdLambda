# Kubernetes install (kustomize)

```
deploy/kustomize/
├── apps/
│   └── server/              # environment-neutral definition of one app
└── overlays/
    └── production/          # namespace, image tags, secrets; composes the apps
```

Each app under `apps/` is self-contained and sets no namespace. An overlay decides which
apps run together and where.

## Prerequisites

- A SQL Server the cluster can reach, with a login allowed to apply schema changes.

## Build the image

Run this from the repository root, after `docker login`. It builds for linux/amd64 and
linux/arm64 and pushes both as one image:

```bash
VERSION=0.1.0 make push
```

`VERSION` must match `newTag` in the overlay.

The image's default command is `serve`. The Deployment's `migrate` init container uses the
same image with `migrate` as its argument. If the registry is private, the pods also need an
`imagePullSecrets` entry.

## Install

1. Set the image name and tag under `images:` in `overlays/production/kustomization.yaml`.
2. Create the secrets file, then edit it with the real connection string:

   ```bash
   cd deploy/kustomize/overlays/production
   cp server.secret.env.example server.secret.env
   ```

3. Review what will be applied, then apply it from the repository root:

   ```bash
   kubectl kustomize deploy/kustomize/overlays/production
   kubectl apply -k deploy/kustomize/overlays/production
   ```

4. Confirm the rollout:

   ```bash
   kubectl -n thirdlambda rollout status deployment/thirdlambda-server
   kubectl -n thirdlambda logs deployment/thirdlambda-server -c migrate
   ```

To see detailed health, forward the management port. The local port must be 8081 because the
health endpoints only answer requests addressed to that port.

```bash
kubectl -n thirdlambda port-forward deployment/thirdlambda-server 8081:8081
curl localhost:8081/healthz
```

## Upgrade

Change `newTag` in the overlay and run `kubectl apply -k` again. Migrations run in the new
pod's `migrate` init container, while the old pod keeps serving. Changing the config or the
secrets file also rolls the pods, because those objects are generated with a content hash.

Other apps in the cluster reach the gRPC API at `thirdlambda-server:3000` from inside the
namespace, or `thirdlambda-server.thirdlambda.svc.cluster.local:3000` from outside it.

## Adding an app

1. Create `apps/<app>/` with its own `kustomization.yaml`, modelled on `apps/server`. Name
   its resources `thirdlambda-<app>` and give its pods a unique `app.kubernetes.io/name`.
2. Add it to `resources:` in the overlay, with an `images:` entry and, if it needs one, a
   `secretGenerator` entry.

Cluster-scoped objects, such as an Operator's CRDs and ClusterRoles, can live in its app
directory too. The overlay's `namespace:` setting only applies to namespaced resources.
