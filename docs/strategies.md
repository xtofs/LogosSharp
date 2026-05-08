
# strategies 

## Lookup Table with Bitsets (Recommended)
Since characters can belong to multiple categories, use bitsets:
rust// Each character maps to a bitset of categories
const CHAR_CATEGORIES: [u32; 256] = [...]; // for ASCII
// Each matcher has a bitset of "acceptable starting categories"
const MATCHER_CATEGORIES: [u32; NUM_MATCHERS] = [...];

// Dispatch
let char_cats = CHAR_CATEGORIES[current_char as usize];
for (i, &matcher_cats) in MATCHER_CATEGORIES.iter().enumerate() {
    if (char_cats & matcher_cats) != 0 {
        if MATCHERS[i](input, cursor) {
            return Some(token);
        }
    }
}
Pros: Simple, cache-friendly, handles multi-category elegantly
Cons: Still iterates, but only over relevant matchers


## Inverted Index Approach
Instead of iterating matchers, pre-compute which matchers to try for each character:
rust// Build once at compile-time or startup
const DISPATCH_TABLE: [&[MatcherFn]; 256] = [...];

// Usage - just iterate the short list
for matcher in DISPATCH_TABLE[current_char as usize] {
    if matcher(input, cursor) {
        return Some(token);
    }
}
For multi-category characters, duplicate the matcher reference in multiple slots. This is what many lexer generators do.

### recommendation: Start with #2 (Inverted Index) if you're building a lexer by hand. It's the sweet spot:
rusttype MatcherFn = fn(&str, &mut usize) -> bool;

// Build this at compile time with a macro or build script
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


## Tiered Dispatch with Fast Path
Most tokens start with very specific characters. Use a two-tier system:
rust
match current_char {
    // Fast path - exact character matches
    '/' => try_matchers(&[comment_matcher, div_matcher]),
    '+' => try_matchers(&[plus_matcher, increment_matcher]),
    
    // Slow path - category-based
    'a'..='z' | 'A'..='Z' | '_' => try_matchers(&IDENTIFIER_MATCHERS),
    '0'..='9' => try_matchers(&NUMBER_MATCHERS),
    
    // Whitespace, operators, etc.
    _ => try_all_matchers(), // or return error
}

## DFA/Trie Hybrid (Most Complex, Fastest)
This is what logos does internally - build a DFA for unambiguous prefixes, fall back to matchers only when needed:
rust// State machine for common prefixes
let state = DFA_TRANSITIONS[state][current_char];
if state.is_final() {
    return state.token_type();
}
if state.needs_matcher() {
    // Only now try the specific matchers
    return try_matchers(state.candidate_matchers());
}