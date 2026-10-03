# Precedence table

| Prec. | Operator        | Associativity              |
|-------|-----------------|----------------------------|
| 1     | f(x)            | postfix                    |
| 2     | unary (- + ! ~) | unary                      |
| 3     | as              | Left                       |
| 4     | * / %           | Left                       |
| 5     | + -             | Left                       |
| 6     | << >> ^ & \|    | Left (mixing is forbidden) |
| 7     | < <= > >= == != | Not associative            |
| 8     | &&              | Left                       |
| 9     | \|\|            | Left                       |
