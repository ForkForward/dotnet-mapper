# How this project is developed

ForkForward keeps open-source libraries alive after their original line of development stops being open. The maintenance work is done with AI coding tools and automation. This document says what that means in practice and who is responsible for what.

## 1. AI tools write the changes

Every change to this repository is produced with an AI coding tool or by automation such as Dependabot.

If you want something fixed, the most useful things you can give are a clear issue, a minimal reproduction, or a failing test. Pull requests are welcome when the change in them was produced the same way, and say so (see section 3).

## 2. People are responsible

AI tools do not merge, release or decide.

- A named maintainer reviews every change before it is merged. The reviewers are listed in [.github/CODEOWNERS](.github/CODEOWNERS).
- A change is merged only when the full test suite passes in CI.
- A maintainer decides what goes into a release and publishes it.
- The maintainer who merges a change is accountable for it, however it was written.

## 3. Every change says how it was made

Each pull request states:

- which AI tool and model produced the change,
- what the person submitting it checked by hand,
- which tests cover it.

Commits written by an AI tool carry a `Co-Authored-By` trailer naming the tool.

## 4. Where code may come from

This is the rule that protects the project and its users. It has no exceptions.

- The baseline is the MIT-licensed AutoMapper code at commit `92816585` (see [NOTICE.md](NOTICE.md)).
- **Nothing is copied from AutoMapper releases published after that commit, or from the `LuckyPennySoftware/AutoMapper` repository.** Those are under a different licence. This covers code, tests, documentation and commit messages.
- Do not give that code to an AI tool as context, and do not ask a tool to reproduce or port it.
- If the official AutoMapper fixes a bug that also exists here, the fix in this fork is written independently, starting from the bug report or a reproduction.
- Third-party code may be added only under a licence compatible with MIT, with its notice kept.

If you are not sure whether something is allowed, ask in an issue before you open the pull request.

## 5. Compatibility comes first

People use this fork so that existing code keeps working.

- The public API stays compatible with AutoMapper 14.0.0. Every build compares the two and writes the differences to `src/AutoMapper/ApiCompatBaseline.txt`. A pull request that changes that file changes the API, and needs an explicit decision by a maintainer.
- The `AutoMapper` namespace is not renamed without a decision recorded in [ROADMAP.md](ROADMAP.md).
- Behaviour changes need a test that shows the old and the new behaviour.
- Speed is part of compatibility. A change must not make the library slower, or make it allocate more memory, than the last release. CI measures both on every pull request that touches the library and again before a release is published; see the Performance section of [CONTRIBUTING.md](CONTRIBUTING.md#performance).

## 6. Credit

The original authors' copyright and licence notices are never removed or edited. Documentation that describes the project says that it is a fork of AutoMapper and links to the original.
