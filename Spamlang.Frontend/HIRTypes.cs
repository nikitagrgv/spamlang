namespace Spamlang.Frontend;

public abstract class HIRNode
{
    public required Node Syntax { get; init; }
    public required bool IsSynthesized { get; init; }
}

public sealed class HIRCompilationUnit : HIRNode
{
    public required IReadOnlyList<HIRFuncDecl> FuncDecls { get; init; }
}

public sealed class HIRFuncDecl : HIRNode
{
    public required FuncSymbol Symbol { get; init; }
    public required IReadOnlyList<VariableSymbol> Locals { get; init; }
    public required HIRBlock Body { get; init; }
}

public sealed class HIRBlock : HIRStmt
{
    public required IReadOnlyList<VariableSymbol> Variables { get; init; }
    public required IReadOnlyList<HIRStmt> Stmts { get; init; }
}

public abstract class HIRStmt : HIRNode
{
}

public sealed class HIRStmtLet : HIRStmt
{
    public required VariableSymbol VariableSymbol { get; init; }
    public required HIRExpr Init { get; init; }
}

public sealed class HIRStmtAssign : HIRStmt
{
    public required HIRExpr Target { get; init; }
    public required HIRExpr Value { get; init; }
}

public sealed class HIRStmtReturn : HIRStmt
{
    public required HIRExpr? Value { get; init; }
}

public sealed class HIRStmtExpr : HIRStmt
{
    public required HIRExpr Expr { get; init; }
}

public abstract class HIRExpr : HIRNode
{
    public required SpamType Type { get; init; }
    public abstract bool IsLValue { get; }
}

public sealed class HIRExprLocalRef : HIRExpr
{
    public required LocalSymbol Symbol { get; init; }
    public override bool IsLValue => true;
}

public sealed class HIRExprFuncRef : HIRExpr
{
    public required FuncSymbol Symbol { get; init; }
    public override bool IsLValue => false;
}

public sealed class HIRExprLoad : HIRExpr
{
    public required HIRExpr Address { get; init; }
    public override bool IsLValue => false;
}

public sealed class HIRExprIntConst : HIRExpr
{
    public required Int128 Value { get; init; }
    public override bool IsLValue => false;
}

public sealed class HIRExprZeroInit : HIRExpr
{
    public override bool IsLValue => false;
}

public sealed class HIRExprUnary : HIRExpr
{
    public required UnaryOp Op { get; init; }
    public required HIRExpr Operand { get; init; }
    public override bool IsLValue => false;
}

public sealed class HIRExprBinary : HIRExpr
{
    public required BinaryOp Op { get; init; }
    public required HIRExpr Left { get; init; }
    public required HIRExpr Right { get; init; }
    public override bool IsLValue => false;
}

public sealed class HIRExprCall : HIRExpr
{
    public required HIRExpr Callee { get; init; }
    public required IReadOnlyList<HIRCallArg> Args { get; init; }
    public override bool IsLValue => false;
}

public sealed class HIRCallArg : HIRNode
{
    public required HIRExpr Value { get; init; }
    public required int ParameterIndex { get; init; }
}

public sealed class HIRExprError : HIRExpr
{
    public required IReadOnlyList<HIRExpr> Children { get; init; }
    public override bool IsLValue => false;
}

public sealed class HIRExprCast : HIRExpr
{
    public required HIRExpr Value { get; init; }
    public override bool IsLValue => false;
}