using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// IDataPersistence works as an interface for different classes to implement their own .
//
// 
public interface IDataPersistence
{
    void LoadData(GameData data);
    void SaveData(ref GameData data);
}
