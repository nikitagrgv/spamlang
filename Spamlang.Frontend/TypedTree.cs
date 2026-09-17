namespace Spamlang.Frontend;

public abstract class TypedNode
{
    public required Node Syntax { get; init; }
    public required bool IsSynthesized { get; init; }
}

public class TypedCompilationUnit : TypedNode
{
    public required IReadOnlyList<TypedFuncDecl> FuncDecls { get; init; }
}

public class TypedFuncDecl : TypedNode
{
    public required FuncSymbol Symbol { get; init; }
    public required IReadOnlyList<VariableSymbol> Variables { get; init; }
    public required TypedBlock Body { get; init; }
}

public class TypedBlock : TypedStmt
{
    public required IReadOnlyList<VariableSymbol> Variables { get; init; }
    public required IReadOnlyList<TypedStmt> Stmts { get; init; }
}

public abstract class TypedStmt : TypedNode
{
}

public class TypedStmtLet : TypedStmt
{
    public required VariableSymbol VariableSymbol { get; init; }
    public required TypedExpr Init { get; init; }
}

public class TypedStmtAssign : TypedStmt
{
    public required TypedExpr Target { get; init; }
    public required TypedExpr Value { get; init; }
}

public class TypedStmtReturn : TypedStmt
{
    public required TypedExpr? Value { get; init; }
}

public class TypedStmtExpr : TypedStmt
{
    public required TypedExpr Expr { get; init; }
}

public abstract class TypedExpr : TypedNode
{
    public required SpamType Type { get; init; }
    public abstract bool IsLValue { get; }
}

public class TypedLocalRef : TypedExpr
{
    public required LocalSymbol Symbol { get; init; }
    public override bool IsLValue => false;
}

public class TypedFuncRef : TypedExpr
{
    public required FuncSymbol Symbol { get; init; }
    public override bool IsLValue => true;
}

public class TypedLoad : TypedExpr
{
    public required TypedExpr Address { get; init; }
    public override bool IsLValue => false;
}

public class TypedIntConst : TypedExpr
{
    public required Int128 Value { get; init; }
    public override bool IsLValue => false;
}

public class TypedZeroInit : TypedExpr
{
    public override bool IsLValue => false;
}

public class TypedUnary : TypedExpr
{
    public required UnaryOp Op { get; init; }
    public required TypedExpr Operand { get; init; }
    public override bool IsLValue => false;
}

public class TypedBinary : TypedExpr
{
    public required BinaryOp Op { get; init; }
    public required TypedExpr Left { get; init; }
    public required TypedExpr Right { get; init; }
    public override bool IsLValue => false;
}

public class TypedCall : TypedExpr
{
    public required TypedExpr Callee { get; init; }
    public required IReadOnlyList<TypedArg> Args { get; init; }
    public override bool IsLValue => false;
}

public class TypedArg : TypedNode
{
    public required TypedExpr Value { get; init; }
    public required int ParameterIndex { get; init; }
}