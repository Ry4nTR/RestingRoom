using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;
using System.Collections;

public class RoomSelector : MonoBehaviour
{
    [SerializeField] private List<Room> rooms;
    private TMP_Dropdown selectorDropdown;

    private void Awake()
    {
        selectorDropdown = GetComponent<TMP_Dropdown>();
        selectorDropdown.onValueChanged.AddListener(ChangeRoom);
    }

    private void Start()
    {
        ChangeRoom(selectorDropdown.value);
    }

    private void OnDestroy()
    {
        selectorDropdown.onValueChanged.RemoveAllListeners();
    }

    public void ChangeRoom(int newValue)
    {
        SetOldRoom(out Room oldRoom);
        Room newRoom = rooms[newValue].gameObject.activeInHierarchy ? rooms[newValue] : null;
        
        StartChangeAnimation(newValue, oldRoom, newRoom);
    }

    private void StartChangeAnimation(int newValue, Room oldRoom, Room newRoom)
    {
        oldRoom.gameObject.SetActive(true);
        newRoom.gameObject.SetActive(true);

        newRoom.transform.rotation = Quaternion.Euler(Vector3.down);


        StartCoroutine(RotationCoroutine(oldRoom, newRoom));

        for (int i = 0; i < rooms.Count; i++)
        {
            if (i == newValue)
            {
                rooms[i].gameObject.SetActive(true);

            }
            else
            {

            }
        }
    }

    IEnumerator RotationCoroutine(Room oldRoom, Room newRoom)
    {
        yield return null;
    }
    private void SetOldRoom(out Room oldRoom)
    {
        oldRoom = null;
        foreach (var room in rooms.Where(room => room.gameObject.activeInHierarchy))
        {
            oldRoom = room;
            break;
        }
    }
}
