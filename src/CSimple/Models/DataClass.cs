
public class DataClass
{
    public List<DataItem> Data { get; set; } = new List<DataItem>();
    public bool DataIsError { get; set; } = false;
    public bool DataIsSuccess { get; set; } = false;
    public bool DataIsLoading { get; set; } = false;
    public string DataMessage { get; set; } = string.Empty;
    public string Operation { get; set; } = null;
}

public class DataItem
{
    public DataContent Data { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Id { get; set; }
    public int Version { get; set; }
}

public class DataContent
{
    public string Text { get; set; }
    public List<FileItem> Files { get; set; } = new List<FileItem>();
}

public class FileItem
{
    public string Filename { get; set; }
    public string ContentType { get; set; }
    public string Data { get; set; }
}
// ideal DataClass = { 
//     user: { 
//         _id: '65673ec1fcacdd019a167520', 
//         nickname: 'tnnrhpwd', 
//         email: 'tnnrhpwd@gmail.com', 
//         token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpZCI6IjY1NjczZWMxZmNhY2RkMDE5YTE2NzUyMCIsImlhdCI6MTcyNTczMTExOCwiZXhwIjoxNzI4MzIzMTE4fQ.f9TqWqfjQfkDdNqk4Y8-rzFobJFz_en8tUI4YwR1rsI' 
//     }, 
//     data: { 
//         entries: [ 
//             { 
//                 text: 'Creator:65673ec1fcacdd019a167520|Goal:Identify the movie with brown hair guy has beach house blown up and loses his guitar on the roof. The movie was made before year 2000', 
//                 files: [ 
//                     { 
//                         filename: 'Creator:65673ec1fcacdd019a167520|Goal:Build a house', 
//                         contentType: 'text/plain', 
//                         data: 'iVBORw0KGgoAAAANSUhEUgAABAAAAAQACAYAAAB/HSuDA' 
//                     } 
//                 ], 
//                 updatedAt: '2021-07-26T18:00:00.000Z', 
//                 createdAt: '2021-07-26T18:00:00.000Z', 
//                 _id: '65673ec1fcacdd019a167520', 
//                 __v: 0 
//             }, 
//             { 
//                 text: 'Creator:65673ec1fcacdd019a167520|Action:{"Id":"0f7dab3a-aaaa-bbbb-cccc-a0f5e0649c7e","ActionName":"Example Action","ActionArray":[{"Timestamp":"2023-10-11T12:34:56Z","KeyCode":49,"EventType":256,"Duration":100,"Coordinates":null}],"ActionModifiers":[],"Creator":"65673ec1fcacdd019a167520","ActionArrayFormatted":"{...}","IsSimulating":false}', 
//                 updatedAt: '2021-07-26T18:00:00.000Z', 
//                 createdAt: '2021-07-26T18:00:00.000Z', 
//                 _id: '65673ec1fcacdd019a167520', 
//                 __v: 0 
//             } 
//         ], 
//         isError: false, 
//         isSuccess: true, 
//         isLoading: false, 
//         message: '', 
//         operation: 'get'
//     } 
// };