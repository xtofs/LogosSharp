using static SExprExtensions;


// Collection expression construction
SList call = [Sym("add"), Int(1), Int(2)];
SExpr expr = call;


Console.WriteLine(expr);

// List pattern matching
var s = expr switch
{
    SList and [Symbol("if"), var cond, var then, var els] => $"if {cond} then {then} else {els}",
    SList and [Symbol("lambda"), SList parms, var body] => $"lambda ({string.Join(", ", parms)}) => {body}",
    SList and [Symbol(var op), .. var opArgs] => $"{op}({string.Join(", ", opArgs)})",
    IntLit i => $"int {i.Value}",
    _ => "?"
};

Console.WriteLine(s);

for (int i = 0; i < 5; i++)
{
    var r = RandomSExpr(Random.Shared, 3, [Sym("a"), Sym("b"), Sym("c")]);
    Console.WriteLine(r);
}


static SExpr RandomSExpr(Random rng, int maxDepth, List<SExpr> atoms)
{
    if (maxDepth == 0)
    {
        return atoms[rng.Next(atoms.Count)];
    }

    // Randomly decide: atom or list?
    if (rng.NextDouble() < 0.3)  // 30% chance of atom
        return atoms[rng.Next(atoms.Count)];

    // Generate a list with random length
    int listLen = rng.Next(0, 5);
    var list = new List<SExpr>();
    for (int i = 0; i < listLen; i++)
    {
        list.Add(RandomSExpr(rng, maxDepth - 1, atoms));
    }
    return new SList(list);
}