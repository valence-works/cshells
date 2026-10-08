# Data Model: Settled Shell Activation Results

## Shell generation

- **Identity**: Shell name and monotonically assigned generation number.
- **Existing lifecycle state**: Candidate moves through initialization to active and may later deactivate, drain, or dispose.
- **New internal activation fact**: A monotonic boolean indicating that activation commit, completion fan-out, and the final published-active eligibility check succeeded.
- **Visibility**: The fact is stored on concrete `Shell`, is initially false, and is not part of public `IShell`.
- **Lifetime**: It is set once for a generation and never cleared, including if later draining/disposal changes shell lifecycle state.

## Activation request

- **Identity**: Caller request for one shell name.
- **Settlement**: Completes with the current committed shell, starts normal activation when no shell is active, or completes with the established failure/cancellation behavior.
- **Wait ownership**: Waiting on the per-name semaphore does not transfer cancellation ownership to the activation currently holding it.
