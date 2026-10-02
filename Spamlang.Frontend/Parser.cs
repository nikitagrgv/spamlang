using System.Diagnostics;

namespace Spamlang.Frontend;

public class Parser
{
    private readonly string _code;
    private readonly Diagnostic _diag;
    private readonly List<Token> _tokens;
    private int _cursor;

    private class UnexpectedTokenException(Token given, TokenType? expected)
        : Exception($"Unexpected token: {given.Type}")
    {
        public Token GivenToken { get; } = given;
        public TokenType? Expected { get; } = expected;
    }

    public Parser(string code, List<Token> tokens, Diagnostic diag)
    {
        _code = code;
        _diag = diag;
        _tokens = tokens;
    }

    public CompilationUnit Run()
    {
        _cursor = 0;
        CompilationUnit compilationUnit = ParseCompilationUnit();
        return compilationUnit;
    }

    public Expr RunExpr()
    {
        _cursor = 0;
        Expr expr = ParseExpr();
        return expr;
    }

    private Token Peek(int n = 0)
    {
        int pos = _cursor + n;
        if (pos >= _tokens.Count)
        {
            pos = _tokens.Count - 1;
        }

        return _tokens[pos];
    }

    private bool Check(TokenType type) => Peek().Type == type;

    private bool TryConsume(TokenType type)
    {
        if (!Check(type))
        {
            return false;
        }

        Advance();
        return true;
    }

    private bool IsAtEnd() => Check(TokenType.Eof);

    private void Advance()
    {
        _cursor++;
        if (_cursor >= _tokens.Count)
        {
            _cursor = _tokens.Count - 1;
        }
    }

    private int Expect(TokenType type)
    {
        int pos = _cursor;
        if (!Check(type))
        {
            Token given = _tokens[_cursor];
            throw new UnexpectedTokenException(given, type);
        }

        Advance();
        return pos;
    }

    private void MustBe(TokenType type)
    {
        if (Check(type))
        {
            return;
        }

        Token given = _tokens[_cursor];
        throw new UnexpectedTokenException(given, type);
    }

    private bool GoTo(int prevCursor, params TokenType[] types)
    {
        Debug.Assert(types.Length > 0 && !types.Contains(TokenType.Eof));

        if (_cursor <= prevCursor && !types.Contains(Peek().Type))
        {
            Advance();
        }

        while (!IsAtEnd())
        {
            if (types.Contains(Peek().Type))
            {
                return true;
            }

            Advance();
        }

        return false;
    }

    private int End(int begin)
    {
        return Math.Max(begin, _cursor - 1);
    }

    private CompilationUnit ParseCompilationUnit()
    {
        int begin = _cursor;

        List<FuncDecl> funcDecls = [];
        while (!IsAtEnd())
        {
            int prevCursor = _cursor;
            try
            {
                FuncDecl decl = ParseFuncDecl();
                funcDecls.Add(decl);
            }
            catch (UnexpectedTokenException e)
            {
                ReportError(e);
                GoTo(prevCursor, TokenType.KeywordFunc);
            }
        }

        int end = End(begin);
        CompilationUnit unit = new()
        {
            StartToken = begin,
            EndToken = end,
            FuncDecls = funcDecls,
        };
        return unit;
    }

    private FuncDecl ParseFuncDecl()
    {
        // TODO: Recovery is far from perfect... But I don't care

        int begin = _cursor;

        Expect(TokenType.KeywordFunc);
        int nameToken = Expect(TokenType.Identifier);

        List<Param> parameters = [];
        Expect(TokenType.LPar);

        bool recoveredToLbrace = false;
        int prevCursor;
        if (!Check(TokenType.RPar))
        {
            while (true)
            {
                prevCursor = _cursor;
                try
                {
                    Param param = ParseParam();
                    parameters.Add(param);
                    if (Check(TokenType.RPar))
                    {
                        break;
                    }

                    Expect(TokenType.Comma);
                }
                catch (UnexpectedTokenException e)
                {
                    ReportError(e);
                    while (true)
                    {
                        prevCursor = _cursor;
                        GoTo(prevCursor, TokenType.Comma, TokenType.RPar, TokenType.LBrace, TokenType.KeywordFunc);
                        if (Check(TokenType.KeywordFunc))
                        {
                            if (Peek(-1).Type == TokenType.Colon)
                            {
                                // fn is used as type declaration, not function
                                Advance();
                                continue;
                            }

                            // Delegate recovery to ParseCompilationUnit(), it will recover to the next function
                            throw new UnexpectedTokenException(Peek(), null);
                        }

                        break;
                    }


                    if (Check(TokenType.LBrace))
                    {
                        recoveredToLbrace = true;
                        break;
                    }

                    if (Check(TokenType.Comma))
                    {
                        Advance();
                        continue;
                    }

                    break;
                }
            }
        }

        prevCursor = _cursor;
        TypeNode? returnType = null;
        try
        {
            Expect(TokenType.RPar);

            if (TryConsume(TokenType.Arrow))
            {
                prevCursor = _cursor;
                returnType = ParseType();
            }
        }
        catch (UnexpectedTokenException e)
        {
            if (!recoveredToLbrace)
            {
                ReportError(e);
                GoTo(prevCursor, TokenType.LBrace, TokenType.KeywordFunc);
                MustBe(TokenType.LBrace);
            }
            // else - already reported
        }

        prevCursor = _cursor;
        if (!Check(TokenType.LBrace))
        {
            Token given = _tokens[_cursor];
            ReportError(given, TokenType.LBrace);
            GoTo(prevCursor, TokenType.LBrace, TokenType.KeywordFunc);
            MustBe(TokenType.LBrace);
        }

        Block body = ParseBlock();

        int end = End(begin);
        return new FuncDecl
        {
            StartToken = begin,
            EndToken = end,
            NameToken = nameToken,
            Params = parameters,
            ReturnType = returnType,
            Body = body,
        };
    }

    private Param ParseParam()
    {
        int begin = _cursor;
        int nameToken = Expect(TokenType.Identifier);
        Expect(TokenType.Colon);
        TypeNode typeNode = ParseType();
        int end = End(begin);
        return new Param
        {
            StartToken = begin,
            EndToken = end,
            NameToken = nameToken,
            Type = typeNode,
        };
    }

    private TypeNode ParseType()
    {
        int begin = _cursor;

        if (TryConsume(TokenType.KeywordFunc))
        {
            List<TypeNode> parameters = [];
            Expect(TokenType.LPar);
            if (!Check(TokenType.RPar))
            {
                do
                {
                    TypeNode type = ParseType();
                    parameters.Add(type);
                } while (TryConsume(TokenType.Comma));
            }

            Expect(TokenType.RPar);

            TypeNode? returnType = null;
            if (TryConsume(TokenType.Arrow))
            {
                returnType = ParseType();
            }

            return new FuncTypeNode
            {
                StartToken = begin,
                EndToken = End(begin),
                Params = parameters,
                ReturnType = returnType,
            };
        }

        if (TryConsume(TokenType.Star))
        {
            TypeNode pointee = ParseType();
            return new PointerTypeNode
            {
                StartToken = begin,
                EndToken = End(begin),
                Pointee = pointee,
            };
        }

        Expect(TokenType.Identifier);
        return new IdentifierTypeNode
        {
            StartToken = begin,
            EndToken = End(begin),
            TypeNameToken = begin,
        };
    }

    private Block ParseBlock()
    {
        int begin = _cursor;
        List<Stmt> stmts = [];

        Expect(TokenType.LBrace);

        while (!Check(TokenType.RBrace) && !IsAtEnd())
        {
            int prevCursor = _cursor;
            try
            {
                Stmt stmt = ParseStmt();
                stmts.Add(stmt);
            }
            catch (UnexpectedTokenException e)
            {
                ReportError(e);
                GoTo(prevCursor,
                    TokenType.Semicolon,
                    TokenType.KeywordLet,
                    TokenType.KeywordReturn,
                    TokenType.LBrace,
                    TokenType.RBrace
                );
                if (Check(TokenType.Semicolon))
                {
                    Advance();
                }
            }
        }

        if (!IsAtEnd())
        {
            Expect(TokenType.RBrace);
        }
        else
        {
            ReportError(Peek(), TokenType.RBrace);
        }

        int end = End(begin);
        return new Block
        {
            StartToken = begin,
            EndToken = end,
            Stmts = stmts,
        };
    }

    private Stmt ParseStmt()
    {
        if (Check(TokenType.LBrace))
        {
            return ParseBlock();
        }

        if (Check(TokenType.KeywordLet))
        {
            return ParseLet();
        }

        if (Check(TokenType.KeywordReturn))
        {
            return ParseReturn();
        }

        return ParseAssignOrStmtExpr();
    }

    private StmtLet ParseLet()
    {
        int begin = _cursor;
        Expect(TokenType.KeywordLet);
        int nameToken = Expect(TokenType.Identifier);

        TypeNode? typeDecl = null;
        if (TryConsume(TokenType.Colon))
        {
            typeDecl = ParseType();
        }

        Expr? expr = null;
        if (typeDecl == null)
        {
            Expect(TokenType.Assign);
            expr = ParseExpr();
        }
        else if (TryConsume(TokenType.Assign))
        {
            expr = ParseExpr();
        }

        Expect(TokenType.Semicolon);

        int end = End(begin);
        return new StmtLet
        {
            StartToken = begin,
            EndToken = end,
            NameToken = nameToken,
            TypeDecl = typeDecl,
            Expr = expr,
        };
    }

    private StmtReturn ParseReturn()
    {
        int begin = _cursor;
        Expr? expr = null;

        Expect(TokenType.KeywordReturn);
        if (!Check(TokenType.Semicolon))
        {
            expr = ParseExpr();
        }

        Expect(TokenType.Semicolon);

        int end = End(begin);
        return new StmtReturn
        {
            StartToken = begin,
            EndToken = end,
            Expr = expr,
        };
    }

    private Stmt ParseAssignOrStmtExpr()
    {
        int begin = _cursor;
        Expr baseExp = ParseExpr();

        Stmt stmt;
        if (Check(TokenType.Assign))
        {
            int op = _cursor;
            Advance();

            Expr value = ParseExpr();
            Expect(TokenType.Semicolon);

            int end = End(begin);
            stmt = new StmtAssign
            {
                StartToken = begin,
                EndToken = end,
                AssignToken = op,
                Target = baseExp,
                Value = value,
            };
        }
        else
        {
            Expect(TokenType.Semicolon);
            int end = End(begin);
            stmt = new StmtExpr
            {
                StartToken = begin,
                EndToken = end,
                Expr = baseExp,
            };
        }

        return stmt;
    }

    private int GetPrecedence(BinaryOp op)
    {
        switch (op)
        {
            case BinaryOp.Mul:
            case BinaryOp.Div:
            case BinaryOp.Rem: return 4;

            case BinaryOp.Plus:
            case BinaryOp.Minus: return 5;

            case BinaryOp.BitAnd:
            case BinaryOp.BitOr:
            case BinaryOp.BitXor:
            case BinaryOp.BitShiftLeft:
            case BinaryOp.BitShiftRight: return 6;

            case BinaryOp.Equal:
            case BinaryOp.NotEqual:
            case BinaryOp.Less:
            case BinaryOp.LessEqual:
            case BinaryOp.Greater:
            case BinaryOp.GreaterEqual: return 7;

            case BinaryOp.LogicAnd: return 8;

            case BinaryOp.LogicOr: return 9;

            default:
                throw new ArgumentOutOfRangeException(nameof(op), op, null);
        }
    }

    // TODO: Use precedence parsing
    private Expr ParseExpr()
    {
        // TODO# WTF
        return ParseExpr(prec: 9);
    }

    private Expr ParseExpr(int prec)
    {
        Debug.Assert(prec > 0);

        if (prec == 1)
        {
            ParsePostfix();
        }

        if (prec == 2)
        {
        }

        int begin = _cursor;
        Expr left = ParseExpr(prec - 1);

        while (true)
        {
            BinaryOp? op = TokenUtils.ToBinaryOp(Peek().Type);
            if (op == null)
            {
                break;
            }

            Advance();

            Expr right = ParseExpr(prec - 1);
            int end = End(begin);

            left = new ExprBinary
            {
                StartToken = begin,
                EndToken = end,
                Left = left,
                Right = right,
                Op = op.Value,
            };
        }

        return left;
    }


    private Expr ParseTerm()
    {
        int begin = _cursor;
        Expr left = ParseCast();
        while (Check(TokenType.Star) ||
               Check(TokenType.Slash) ||
               Check(TokenType.Percent))
        {
            int opPos = _cursor;
            Advance();

            Expr right = ParseCast();
            int end = End(begin);

            left = new ExprBinary
            {
                StartToken = begin,
                EndToken = end,
                Left = left,
                Right = right,
                Op = TokenUtils.ToBinaryOp(_tokens[opPos].Type)!.Value,
            };
        }

        return left;
    }

    private Expr ParseCast()
    {
        int begin = _cursor;
        Expr value = ParseUnary();
        while (TryConsume(TokenType.KeywordAs))
        {
            TypeNode target = ParseType();
            int end = End(begin);
            value = new ExprCast
            {
                StartToken = begin,
                EndToken = end,
                Value = value,
                TargetType = target,
            };
        }

        return value;
    }

    private Expr ParseUnary()
    {
        int begin = _cursor;
        bool numeric = TryConsume(TokenType.Plus) || TryConsume(TokenType.Minus);
        if (numeric || TryConsume(TokenType.Exclamation))
        {
            int opPos = _cursor - 1;
            TokenType opTokType = _tokens[opPos].Type;
            bool negated = opTokType == TokenType.Minus;

            if (numeric)
            {
                if (TryConsume(TokenType.LiteralInt))
                {
                    return new ExprIntConst
                    {
                        StartToken = begin,
                        EndToken = End(begin),
                        LiteralToken = _cursor - 1,
                        IsNegative = negated,
                    };
                }

                if (TryConsume(TokenType.LiteralFloat))
                {
                    return new ExprFloatConst
                    {
                        StartToken = begin,
                        EndToken = End(begin),
                        LiteralToken = _cursor - 1,
                        IsNegative = negated,
                    };
                }
            }

            Expr expr = ParseUnary();
            return new ExprUnary
            {
                StartToken = begin,
                EndToken = End(begin),
                Operand = expr,
                Op = TokenUtils.ToUnaryOp(opTokType)!.Value,
            };
        }

        return ParsePostfix();
    }

    private Expr ParsePostfix()
    {
        int begin = _cursor;
        Expr callee = ParsePrimary();

        while (TryConsume(TokenType.LPar))
        {
            List<CallArg> args = [];
            if (!Check(TokenType.RPar))
            {
                do
                {
                    int argBegin = _cursor;
                    int? argNameToken = null;
                    // Named arg
                    if (Peek().Type == TokenType.Identifier && Peek(1).Type == TokenType.Colon)
                    {
                        argNameToken = Expect(TokenType.Identifier);
                        Expect(TokenType.Colon);
                    }

                    Expr expr = ParseExpr();
                    CallArg arg = new()
                    {
                        StartToken = argBegin,
                        EndToken = End(argBegin),
                        Value = expr,
                        ArgNameToken = argNameToken,
                    };
                    args.Add(arg);
                } while (TryConsume(TokenType.Comma));
            }

            Expect(TokenType.RPar);

            int end = End(begin);
            callee = new ExprCall
            {
                StartToken = begin,
                EndToken = end,
                Callee = callee,
                Args = args,
            };
        }

        return callee;
    }

    private Expr ParsePrimary()
    {
        int begin = _cursor;
        if (TryConsume(TokenType.LiteralInt))
        {
            int end = End(begin);
            return new ExprIntConst
            {
                StartToken = begin,
                EndToken = end,
                LiteralToken = begin,
                IsNegative = false,
            };
        }

        if (TryConsume(TokenType.LiteralFloat))
        {
            int end = End(begin);
            return new ExprFloatConst
            {
                StartToken = begin,
                EndToken = end,
                LiteralToken = begin,
                IsNegative = false,
            };
        }

        if (TryConsume(TokenType.LiteralBool))
        {
            int end = End(begin);
            return new ExprBoolConst
            {
                StartToken = begin,
                EndToken = end,
                LiteralToken = begin,
            };
        }

        if (TryConsume(TokenType.LPar))
        {
            Expr expr = ParseExpr();
            Expect(TokenType.RPar);
            return expr;
        }

        if (Check(TokenType.Identifier))
        {
            return ParseIdentifier();
        }

        Token token = _tokens[_cursor];
        throw new UnexpectedTokenException(token, null);
    }

    private Expr ParseIdentifier()
    {
        int begin = _cursor;
        int identifierToken = Expect(TokenType.Identifier);
        int end = End(begin);
        return new ExprIdentifier
        {
            StartToken = begin,
            EndToken = end,
            IdentifierToken = identifierToken,
        };
    }

    private void ReportError(UnexpectedTokenException e)
    {
        ReportError(e.GivenToken, e.Expected);
    }

    private void ReportError(Token given, TokenType? expected = null)
    {
        string message = UnexpectedTokenMessage(given, expected);
        _diag.AddError(message, given);
    }

    private string UnexpectedTokenMessage(Token given, TokenType? expected = null)
    {
        string value = given.Value(_code).ToString();
        if (value.Length <= 0)
        {
            value = given.Type.PrettyName();
        }

        string str = $"Unexpected token: {value}";
        if (expected != null)
        {
            str += ". Expected " + expected.Value.ErrorMessageName();
        }

        return str;
    }
}