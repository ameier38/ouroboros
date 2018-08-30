module Common.Operators

/// Between operator
let (>=<) x (min, max) = (x >= min) && (x <= max)
