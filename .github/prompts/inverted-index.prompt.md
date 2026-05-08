---
name: inverted-index
description: Refactor to an inverted index approach
---

<!-- Tip: Use /create-prompt in chat to generate content with agent assistance -->

The whole idea of AsciiCharClassBuilder got mangled in my head and we need to clean this up.

What the whole idea was is the following

We are creating the matchers out of the Regex- and Token- attributes
The TryGetNext method needs to iterate over the Matchers and essentially find the one that does match something and make a token out of it.

the naive approach is to try all of them for each "starting character"

## Inverted Index Approach

Instead of iterating all matchers, use this flow:

1. Build a dispatch table once at compile time (or at startup).
2. Map each starting character to the ordered list of matchers that can start with that character.
3. At runtime, iterate only that short list and return the first successful match.

in RUST (sorry, please translate to C#)

```
const DISPATCH_TABLE: [&[MatcherFn]; 256] = [...];
```

and on the usare side - just iterate the short list

```
for matcher in DISPATCH_TABLE[current_char as usize] {
    if matcher(input, cursor) {
        return Some(token);
    }
}
```

For multi-category characters, duplicate the matcher reference in multiple slots.

Build this at compile time i.e. through the source generator (rust again for ilustration only)

```rust
type MatcherFn = fn(&str, &mut usize) -> bool;

const DISPATCH: [&[MatcherFn]; 128] = {
    let mut table = [&[] as &[MatcherFn]; 128];

    // 'a'-'z', 'A'-'Z', '_' -> identifier matchers
    table[b'a' as usize] = &[ident_matcher, keyword_matcher];
    // ... repeat for all relevant chars

    // '0'-'9' -> number matchers
    table[b'0' as usize] = &[int_matcher, float_matcher, hex_matcher];

    // '/' -> comment or division
    table[b'/' as usize] = &[comment_matcher, div_matcher];

    table
};

Can you please clean up the generator,

while doing so please separate the attribute collection (i.e. construction of the LogosEnumModel) from generation of the source.
```
