# Precedence table

| Prec. | Operation                                    | Associativity              |
|-------|----------------------------------------------|----------------------------|
| 0     | primary (literals, identifiers, parentheses) | -                          |
| 1     | postfix (calls)                              | postfix                    |
| 2     | unary (- + ! ~)                              | unary                      |
| 3     | as                                           | Left                       |
| 4     | * / %                                        | Left                       |
| 5     | + -                                          | Left                       |
| 6     | << >> ^ & \|                                 | Left (mixing is forbidden) |
| 7     | < <= > >= == !=                              | Not associative            |
| 8     | &&                                           | Left                       |
| 9     | \|\|                                         | Left                       |
