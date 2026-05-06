# Core Compilation Strategy

The key insight: since there is no backtracking in the currently supported feature set,
each regex element maps directly to a deterministic C# code structure.

The matcher signature is:

```csharp
public static bool Match(ReadOnlySpan<char> src, ref int i)
```

- `i` is the caller-owned index.
- compiled matcher uses a local `pos` while matching.
- on success: `i = pos; return true;`
- on failure: `i` is unchanged and `return false;`

```csharp
// INPUT: regex pattern
// OUTPUT: direct C# matcher code
RegexPattern ast = RegexPattern.Parse(pattern);
EmitMatcher(ast);
```

## Character Classes -> Boolean Conditions

```csharp
// Regex: [a-z0-9_]
if (!(pos < src.Length &&
      (src[pos] is >= 'a' and <= 'z' ||
       src[pos] is >= '0' and <= '9' ||
       src[pos] == '_')))
{
    return false;
}

pos += 1;
```

```csharp
// Regex: [^abc]
if (!(pos < src.Length && !(src[pos] == 'a' || src[pos] == 'b' || src[pos] == 'c')))
{
    return false;
}

pos += 1;
```

## Kleene Star (*) -> While Loop

```csharp
// Regex: a*
while (pos < src.Length && src[pos] == 'a')
{
    pos += 1;
}
// matched 0 or more
```

```csharp
// Regex: [0-9]*
while (pos < src.Length && src[pos] is >= '0' and <= '9')
{
    pos += 1;
}
```

## Plus (+) -> Must-Match-Then-Loop

```csharp
// Regex: a+
if (!(pos < src.Length && src[pos] == 'a'))
{
    return false;
}

pos += 1;

while (pos < src.Length && src[pos] == 'a')
{
    pos += 1;
}
```

## Optional (?) -> Conditional

```csharp
// Regex: a?
if (pos < src.Length && src[pos] == 'a')
{
    pos += 1;
}
// otherwise consume nothing
```

## Sequences -> Sequential Checks

```csharp
// Regex: abc
if (!(pos < src.Length && src[pos] == 'a')) return false;
pos += 1;

if (!(pos < src.Length && src[pos] == 'b')) return false;
pos += 1;

if (!(pos < src.Length && src[pos] == 'c')) return false;
pos += 1;
```

## Alternation (|)

Alternation is shown here for completeness, but is not implemented in the current parser/compiler.

```csharp
// Regex: cat|dog
int checkpoint = pos;

bool matched = TryCat(ref pos);
if (!matched)
{
    pos = checkpoint;
    matched = TryDog(ref pos);
}

if (!matched)
{
    return false;
}
```