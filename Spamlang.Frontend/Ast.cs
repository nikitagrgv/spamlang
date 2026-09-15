namespace Spamlang.Frontend;

public abstract class Node
{
    public required int StartToken { get; init; }
    public required int EndToken { get; init; } // inclusive
}

public sealed class CompilationUnit : Node
{
    public required List<FuncDecl> FuncDecls { get; init; }

    public Scope? Scope { get; set; }
}

public sealed class FuncDecl : Node
{
    public required int NameToken { get; init; }
    public required List<Param> Params { get; init; }
    public required TypeNode? ReturnType { get; init; }
    public required Block Body { get; init; }

    public FuncSymbol? Symbol { get; set; }
}

public sealed class Param : Node
{
    public required int NameToken { get; init; }
    public required TypeNode Type { get; init; }

    public ParamSymbol? Symbol { get; set; }
}

public abstract class TypeNode : Node
{
    public SpamType? ResolvedType { get; set; }
}

public sealed class FuncTypeNode : TypeNode
{
    public required List<TypeNode> Params { get; init; }
    public required TypeNode? ReturnType { get; init; }
}

public sealed class IdentifierTypeNode : TypeNode
{
    public required int TypeNameToken { get; init; }
}

public sealed class PointerTypeNode : TypeNode
{
    public required TypeNode Pointee { get; init; }
}

public abstract class Stmt : Node
{
}

public sealed class Block : Stmt
{
    public required List<Stmt> Stmts { get; init; }

    public Scope? Scope { get; set; }
}

public sealed class StmtLet : Stmt
{
    public required int NameToken { get; init; }
    public required TypeNode? TypeDecl { get; init; }
    public required Expr? Expr { get; set; }

    public VariableSymbol? Symbol { get; set; }
}

public sealed class StmtReturn : Stmt
{
    public required Expr? Expr { get; set; }
}

public sealed class StmtAssign : Stmt
{
    public required int AssignToken { get; init; }
    public required Expr Target { get; init; }
    public required Expr Value { get; set; }
}

public sealed class StmtExpr : Stmt
{
    public required Expr Expr { get; init; }
}

public abstract class Expr : Node
{
    public SpamType? ResolvedType { get; set; }
    public ValueCategory? ValueCategory { get; set; }
}

public sealed class ExprBinary : Expr
{
    public required BinaryOp Op { get; init; }
    public required Expr Left { get; set; }
    public required Expr Right { get; set; }
}

public sealed class ExprUnary : Expr
{
    public required UnaryOp Op { get; init; }
    public required Expr Expr { get; set; }
}

public abstract class ExprPrimary : Expr
{
}

public sealed class ExprInt : ExprPrimary
{
    public required int LiteralToken { get; init; }
    public required bool IsNegative { get; init; }

    public Int128 Value { get; set; }
}

public sealed class ExprIdentifier : ExprPrimary
{
    public required int IdentifierToken { get; init; }

    public Symbol? Symbol { get; set; }
}

public sealed class ExprCallArg : Node
{
    public required Expr Expr { get; set; }
    public required int? ArgNameToken { get; init; }

    public int? ParameterIndex { get; set; }
}

public sealed class ExprCall : ExprPrimary
{
    public required Expr Callee { get; init; }
    public required List<ExprCallArg> Args { get; init; }
}

public abstract class ExprCast : Expr
{
    public required Expr Operand { get; init; }
    public required SpamType Target { get; init; }
}

public sealed class ExprImplicitCast : ExprCast
{
}

// TODO: Add explicit cast