using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Maui.Storage;
using System.Diagnostics;

namespace CSimple.Services
{
    public class ObserveDataService
    {
        #region Properties
        private readonly FileService _fileService;
        private readonly DataService _dataService;
        #endregion

        public ObserveDataService()
        {
            _fileService = new FileService();
            _dataService = new DataService();
        }

        public void AddFileToDataItem(DataItem dataItem, string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || dataItem == null) return;

            if (dataItem.Data.Files == null)
                dataItem.Data.Files = new List<CSimple.ActionFile>();

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var contentType = extension == ".wav" ? "audio/wav" : "image/png";

            if (!File.Exists(filePath))
            {
                File.WriteAllBytes(filePath, Convert.FromBase64String(File.ReadAllText(filePath)));
            }

            dataItem.Data.Files.Add(new CSimple.ActionFile
            {
                Filename = Path.GetFileName(filePath),
                ContentType = contentType,
                Data = Convert.ToBase64String(File.ReadAllBytes(filePath))
            });

            Debug.Print($"File added to data item: {filePath}");
        }

        public async Task SaveDataItemsToFile(IEnumerable<DataItem> dataItems)
        {
            try
            {
                // Check if we have any items to save
                var itemsList = dataItems.ToList();
                if (itemsList.Count == 0)
                {
                    Debug.Print("No data items to save");
                    return;
                }

                // Ensure all items have the IsLocal flag properly set
                foreach (var item in itemsList)
                {
                    if (item?.Data?.ActionGroupObject != null)
                    {
                        // If this is called from ObservePage, assume it's a local action
                        item.Data.ActionGroupObject.IsLocal = true;

                        // Log keyboard events for debugging purposes
                        if (item.Data.ActionGroupObject.ActionArray != null)
                        {
                            int keyDownEvents = item.Data.ActionGroupObject.ActionArray.Count(a => a.EventType == 0x0100);
                            int keyUpEvents = item.Data.ActionGroupObject.ActionArray.Count(a => a.EventType == 0x0101);

                            if (keyDownEvents > 0 || keyUpEvents > 0)
                            {
                                Debug.Print($"Action '{item.Data.ActionGroupObject.ActionName}' contains {keyDownEvents} DOWN and {keyUpEvents} UP keyboard events");
                            }
                        }
                    }
                }

                await _fileService.SaveDataItemsAsync(itemsList);
                Debug.Print($"Saved {itemsList.Count} data items to file");
            }
            catch (Exception ex)
            {
                Debug.Print($"Error saving data items: {ex.Message}");
                Debug.Print(ex.StackTrace);
            }
        }

        public async Task<List<DataItem>> LoadDataItemsFromFile()
        {
            try
            {
                var loadedItems = await _fileService.LoadDataItemsAsync();
                Debug.Print("Data items loaded from file");
                return loadedItems;
            }
            catch (Exception ex)
            {
                Debug.Print($"Error loading data items: {ex.Message}");
                return new List<DataItem>();
            }
        }

        public async Task SaveLocalRichDataAsync(IEnumerable<DataItem> dataItems)
        {
            try
            {
                // Check if we have any items to save
                var itemsList = dataItems.ToList();
                if (itemsList.Count == 0)
                {
                    Debug.Print("No local rich data items to save");
                    return;
                }

                // Log some details about what we're saving
                var actionNames = itemsList.Select(i => i.Data?.ActionGroupObject?.ActionName).ToList();
                Debug.Print($"Saving {itemsList.Count} items to local rich data: {string.Join(", ", actionNames)}");

                // Verify keyboard events are included before saving
                foreach (var item in itemsList)
                {
                    if (item?.Data?.ActionGroupObject?.ActionArray != null)
                    {
                        int keyDownEvents = item.Data.ActionGroupObject.ActionArray.Count(a => a.EventType == 0x0100);
                        int keyUpEvents = item.Data.ActionGroupObject.ActionArray.Count(a => a.EventType == 0x0101);

                        if (keyDownEvents > 0 || keyUpEvents > 0)
                        {
                            Debug.Print($"Action '{item.Data.ActionGroupObject.ActionName}' contains {keyDownEvents} DOWN and {keyUpEvents} UP keyboard events");
                        }
                    }
                }

                // Use the updated SaveLocalDataItemsAsync method to append data
                await _fileService.SaveLocalDataItemsAsync(itemsList);
                Debug.Print("Local rich data saved successfully");
            }
            catch (Exception ex)
            {
                Debug.Print($"Error saving local rich data: {ex.Message}");
                Debug.Print(ex.StackTrace);
            }
        }

        public async Task<List<DataItem>> LoadLocalRichDataAsync()
        {
            try
            {
                var localData = await _fileService.LoadLocalDataItemsAsync();
                Debug.Print("Local rich data loaded");
                return localData;
            }
            catch (Exception ex)
            {
                Debug.Print($"Error loading local rich data: {ex.Message}");
                return new List<DataItem>();
            }
        }

        public DataItem CreateOrUpdateActionItem(string actionName, ActionItem actionItem, string modifierName, string description, int priority)
        {
            if (string.IsNullOrEmpty(actionName)) return null;

            var actionModifier = new ActionModifier
            {
                ModifierName = modifierName,
                Description = description,
                Priority = priority
            };

            // Create the action group object
            var actionGroup = new ActionGroup
            {
                ActionName = actionName,
                ActionArray = new List<ActionItem> { actionItem },
                ActionModifiers = new List<ActionModifier> { actionModifier },
                IsSimulating = false
            };

            var newDataItem = new DataItem
            {
                Data = new DataObject
                {
                    Text = $"Action: {actionName}",
                    ActionGroupObject = actionGroup,
                    Files = new List<CSimple.ActionFile>()
                }
            };

            return newDataItem;
        }

        public async Task CompressAndUploadAsync(List<DataItem> data)
        {
            try
            {
                var token = await SecureStorage.GetAsync("userToken");
                if (string.IsNullOrEmpty(token))
                {
                    Debug.Print("No user token found for upload");
                    return;
                }

                var compressedData = CompressData(data);

                bool meetsCriteria = CheckPriority(compressedData);

                if (meetsCriteria && NetworkIsSuitable())
                {
                    // await _dataService.CreateDataAsync(compressedData, token);
                    Debug.Print("Data uploaded to server");
                }
                else
                {
                    Debug.Print("Data stored locally (network unsuitable or priority too low)");
                    // StoreDataLocally(compressedData);
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"Error in compress and upload: {ex.Message}");
            }
        }

        private object CompressData(List<DataItem> dataItems)
        {
            // Simplified compression implementation
            return dataItems;
        }

        private bool CheckPriority(object compressedData)
        {
            return true;
        }

        private bool NetworkIsSuitable()
        {
            return false;
        }

        public async Task<bool> IsUserLoggedInAsync()
        {
            try
            {
                var userToken = await SecureStorage.GetAsync("userToken");
                return !string.IsNullOrEmpty(userToken);
            }
            catch (Exception ex)
            {
                Debug.Print($"Error checking login status: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteDataItemAsync(DataItem dataItem)
        {
            if (dataItem == null) return false;

            try
            {
                // Gather identifying information to delete from all locations
                List<string> idList = new List<string>();
                List<string> nameList = new List<string>();

                if (!string.IsNullOrEmpty(dataItem._id))
                    idList.Add(dataItem._id);

                if (dataItem?.Data?.ActionGroupObject?.ActionName != null)
                    nameList.Add(dataItem.Data.ActionGroupObject.ActionName);

                // Delete from all storage locations
                await _fileService.DeleteDataItemsAsync(idList, nameList);

                Debug.Print($"Data item completely deleted: {dataItem.Data?.ActionGroupObject?.ActionName ?? dataItem._id}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.Print($"Error deleting data item: {ex.Message}");
                return false;
            }
        }
    }
}
