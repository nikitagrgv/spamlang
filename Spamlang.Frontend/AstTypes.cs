namespace Spamlang.Frontend;

public abstract class Node
{
    public required int StartToken { get; init; }
    public required int EndToken { get; init; } // inclusive
}

public sealed class CompilationUnit : Node
{
    public required IReadOnlyList<FuncDecl> FuncDecls { get; init; }
}

public sealed class FuncDecl : Node
{
    public required int NameToken { get; init; }
    public required IReadOnlyList<Param> Params { get; init; }
    public required TypeNode? ReturnType { get; init; }
    public required Block Body { get; init; }
}

public sealed class Param : Node
{
    public required int NameToken { get; init; }
    public required TypeNode Type { get; init; }
}

public abstract class TypeNode : Node
{
}

public sealed class FuncTypeNode : TypeNode
{
    public required IReadOnlyList<TypeNode> Params { get; init; }
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
    public required IReadOnlyList<Stmt> Stmts { get; init; }
}

public sealed class StmtLet : Stmt
{
    public required int NameToken { get; init; }
    public required TypeNode? TypeDecl { get; init; }
    public required Expr? Expr { get; init; }
}

public sealed class StmtReturn : Stmt
{
    public required Expr? Expr { get; init; }
}

public sealed class StmtAssign : Stmt
{
    public required int AssignToken { get; init; }
    public required Expr Target { get; init; }
    public required Expr Value { get; init; }
}

public sealed class StmtExpr : Stmt
{
    public required Expr Expr { get; init; }
}

public abstract class Expr : Node
{
}

public sealed class ExprBinary : Expr
{
    public required BinaryOp Op { get; init; }
    public required Expr Left { get; init; }
    public required Expr Right { get; init; }
}

public sealed class ExprUnary : Expr
{
    public required UnaryOp Op { get; init; }
    public required Expr Operand { get; init; }
}

public sealed class ExprCast : Expr
{
    public required Expr Value { get; init; }
    public required TypeNode TargetType { get; init; }
}

public abstract class ExprPrimary : Expr
{
}

public sealed class ExprIntConst : ExprPrimary
{
    public required int LiteralToken { get; init; }
    public required bool IsNegative { get; init; }
}

public sealed class ExprFloatConst : ExprPrimary
{
    public required int LiteralToken { get; init; }
    public required bool IsNegative { get; init; }
}

public sealed class ExprBoolConst : ExprPrimary
{
    public required int LiteralToken { get; init; }
}

public sealed class ExprIdentifier : ExprPrimary
{
    public required int IdentifierToken { get; init; }
}

public sealed class CallArg : Node
{
    public required Expr Value { get; init; }
    public required int? ArgNameToken { get; init; }
}

public sealed class ExprCall : ExprPrimary
{
    public required Expr Callee { get; init; }
    public required IReadOnlyList<CallArg> Args { get; init; }
}