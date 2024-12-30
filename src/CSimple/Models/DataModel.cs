using System;
using System.Collections.Generic;
using System.ComponentModel;
public class DataModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    public class User
    {
        public string Id { get; set; }
        public string Nickname { get; set; }
        public string Email { get; set; }
        public string Token { get; set; }
    }
    public List<DataItem> DataItems { get; set; } = new List<DataItem>();
    public bool DataIsError { get; set; }
    public bool DataIsSuccess { get; set; }
    public bool DataIsLoading { get; set; }
    public string DataMessage { get; set; }
    public string Operation { get; set; }
    public List<DataItem> entries { get; set; } = new List<DataItem>();
    public bool isError { get; set; }
    public bool isSuccess { get; set; }
    public bool isLoading { get; set; }
    public string message { get; set; }
    public string operation { get; set; }
}

public class DataItem {
    public string text { get; set; }
    public List<FileItem> files { get; set; } = new List<FileItem>();
    public ActionGroup ActionGroup { get; set; }
    public DateTime updatedAt { get; set; }
    public DateTime createdAt { get; set; }
    public string _id { get; set; }
    public int __v { get; set; }
}

public class FileItem {
    public string Filename { get; set; }
    public string ContentType { get; set; }
    public string Data { get; set; }
}

public class ActionGroup : DataModel {
    private bool _isSimulating = false;
    public Guid Id { get; set; } = Guid.NewGuid(); // Unique identifier for each ActionGroup
    public string ActionName { get; set; }
    public List<ActionItem> ActionArray { get; set; } = new List<ActionItem>();
    public List<ActionModifier> ActionModifiers { get; set; } = new List<ActionModifier>();
    public string Creator { get; set; }
    public string ActionArrayFormatted { get; set; }
    public bool IsSimulating
    {
        get => _isSimulating;
        set
        {
            if (_isSimulating != value)
            {
                _isSimulating = value;
                OnPropertyChanged(nameof(IsSimulating));
            }
        }
    }
}

public class ActionItem {
    public DateTime Timestamp { get; set; }
    public ushort KeyCode { get; set; } // Key Code: 49 for execute key press
    public int EventType { get; set; } // Event type: 0x0000 for keydown
    public int Duration { get; set; } // Duration: key press duration in milliseconds
    public class Coordinates // Optional, used for mouse events
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}

public class ActionModifier
{
    public string ModifierName { get; set; } // Example: "DelayModifier"
    public string Description { get; set; } // Example: "Adds a delay before executing the action"
    public int Priority { get; set; } // Example: 1 (Higher priority modifiers are applied first)
    public Func<ActionItem, int> Condition { get; set; } // Example: item => item.KeyCode == 49 (Apply only if the KeyCode is 49)
    public Action<ActionItem> ModifyAction { get; set; } // Example: item => item.Duration += 1000 (Add 1000 milliseconds to the duration)
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
//                 ActionGroup: {
//                     Id: '0f7dab3a-aaaa-bbbb-cccc-a0f5e0649c7e',
//                     ActionName: 'Example Action',
//                     ActionArray: [
//                         {    
//                             Timestamp: '2023-10-11T12:34:56Z',
//                             KeyCode: 49,
//                             EventType: 256,
//                             Duration: 100,
//                             Coordinates: null
//                         }
//                     ],
//                     ActionModifiers: [],
//                     Creator: '65673ec1fcacdd019a167520',
//                     ActionArrayFormatted: '{...}',
//                     IsSimulating: false
//                 },
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