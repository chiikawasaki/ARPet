using UnityEngine;
using Meta.XR.MRUtilityKit;
using System.Reflection;

public class MRUKRoomInspector : MonoBehaviour
{
    void Start()
    {
        if (MRUK.Instance == null)
        {
            Debug.LogError("MRUK.Instance is NULL");
            return;
        }

        var rooms = MRUK.Instance.Rooms;
        if (rooms == null || rooms.Count == 0)
        {
            Debug.LogError("MRUK.Instance.Rooms is EMPTY");
            return;
        }

        Debug.Log("===== MRUK ROOM INSPECTOR =====");
        Debug.Log($"Room count: {rooms.Count}");

        var room = rooms[0];
        var type = room.GetType();
        Debug.Log("MRUKRoom Type: " + type);

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            Debug.Log("ROOM PROPERTY: " + prop.Name + " (" + prop.PropertyType + ")");

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            Debug.Log("ROOM FIELD: " + field.Name + " (" + field.FieldType + ")");
    }
}
