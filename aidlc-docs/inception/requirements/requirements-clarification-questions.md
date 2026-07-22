# Requirements Clarification Questions (Round 2)

I detected one ambiguity in your answers that needs resolving before I write `requirements.md`. The other 9 answers were clear and are already recorded.

## Ambiguity 1: Non-sandboxed behavior/performance parity (Q7)
You selected both "A" (zero behavior/performance change required) and "B" (minor differences acceptable if justified), with the note that this build's bash will be used *only* within AppContainer, since you already have a separate normal-bash install. That context suggests the strict zero-change bar from A may not actually matter much to you — but it could also mean you just want confirmation that a minimal, justified check is fine and don't feel strongly. I want to pin down which, since it affects how cautious the patch needs to be in the non-AppContainer code path (and whether that path needs dedicated regression verification at all).

### Clarification Question 1 — RESOLVED (see below, no further answer needed)

**Note**: The user correctly flagged that options A and B in this question were not meaningfully distinct (both allowed minor changes). Rather than ask a third confusing round for a low-stakes decision, resolved by engineering judgment per the user's stated context:

**Resolution**: Minor, justified differences in the non-AppContainer startup path are acceptable (e.g., one cheap token-type check added at startup). No dedicated zero-diff regression suite is required for that path; the basic bash smoke tests requested in Q10 are sufficient coverage. User was given the chance to object and did not.
