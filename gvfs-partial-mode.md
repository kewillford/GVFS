GVFS: Partial Mode
==================

Basic Idea
----------

Drop the virtualization layer: no filter driver at all!

Only use sparse-checkout to limit the working directory.
Requires users to manually set the size of the working
directory via `gvfs add` and `gvfs remove`. These need
polish to be usable by tools (specifically, get files
and folders via `stdin`).

We operate very similarly to partial clone, except we
use the read-object hook and the GVFS protocol for
object downloads (including `gvfs prefetch --commits`
in the background).

How to Test Manually
--------------------

1. Unmount all existing repos.

1. Build and install based on the test branch.

1. `gvfs clone [url] [folder]`

1. `cd folder/src`

1. `gvfs add --folders="folder1;folder2"`

1. Test Git operations, like `git add`, `git checkout`, etc.

1. Remember to unmount and delete the test repo before re-installing the normal product.

Summary of Changes
------------------

* Drop all hooks except `read-object` hook.

* No registering with the driver, and `InProcessMount`
does not create a `FileSystemCallbacks` instance.

* No background operations to "catch up" after an
index change.

* No placeholder database or modified paths list.
This means the heartbeat is currently useless, and
some of the named pipe messages are not worth keeping.

* New `add` and `remove` verbs add a folder prefix to
the `sparse-checkout` file. The `add` verb also prefetches
the necessary blobs (and hydrates at this time).

* A small change in the blob prefetcher is required
because of a hack using the `sparse-checkout` file for
FastFetch.

* `IPlatformFileSystem.HydrateFiles()` doesn't work if
you just read the first byte! Actually read the Git data
and write it to the file.

Big TODOs
---------

* How do we distribute partial mode?

**Option 1:** Ship GVFS in total, and have an option
at clone time. Hard to justify shipping this when the
"full" version is not available. Also: can we avoid
the complications of shipping a driver at all?

**Option 2:** Ship a completely different tool with
a separate command-line interface and installer.
Requires some duplication of boilerplate and combining
of stuff into object libraries.

* WE NEED TESTS! This is something that is hard to
even start if we don't know how we are distributing it.

* `git read-tree -m -u HEAD` is super-slow on Windows
for large `.git/index` files due to slow `lstat()`
calls. This seems to be avoided by using the prefetch to
hydrate, but it doesn't fix any following `git reset --hard`.
More work is required here.

* On the plus side, `git checkout` seems to be fast
enough if we don't need to hydrate a bunch of files.
(`git checkout HEAD~1000` was pretty fast on AzureDevOps).

* Where are our new performance bottlenecks? All of
our work around O(Modified) is now O(Hydrated), for
one.

Small TODOS
-----------

* The pre-command hook triggers `gvfs prefetch --commits`
during a `git fetch`. We will probably want to run that
directly from Git, if a config setting exists. Background
prefetch only runs if a cache server exists.

