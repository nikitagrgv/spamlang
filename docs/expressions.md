# Precedence table

| Prec. | Operator        | Associativity              |
|-------|-----------------|----------------------------|
| 9     | f(x)            | postfix                    |
| 8     | unary (- + ! ~) | unary                      |
| 7     | as              | Left                       |
| 6     | * / %           | Left                       |
| 5     | + -             | Left                       |
| 4     | << >> ^ & \|    | Left (mixing is forbidden) |
| 3     | < <= > >= == != | Not associative            |
| 2     | &&              | Left                       |
| 1     | \|\|            | Left                       |
