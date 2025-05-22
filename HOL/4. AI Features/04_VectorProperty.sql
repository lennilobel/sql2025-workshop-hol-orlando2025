
SELECT 
    Id,
    VECTORPROPERTY(RawVector, 'Dimension') AS Dimension,
    VECTORPROPERTY(RawVector, 'Length') AS Length,
    VECTORPROPERTY(RawVector, 'DataType') AS DataType
FROM DemoVectors;
