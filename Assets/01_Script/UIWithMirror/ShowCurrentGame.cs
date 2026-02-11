using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShowCurrentGame : MonoBehaviour
{
    [SerializeField] Text hostScore;
    [SerializeField] Text clientScore;
    [SerializeField] Text hostHealth;
    [SerializeField] Text clientHealth;
    [SerializeField] Text TimeLeft;

    public void ChangeHostScore(int score)
    {
        hostScore.text = $"호스트 스코어 : {score}";
    }
    public void ChangeClientScore(int score)
    {
        clientScore.text = $"클라 스코어 : {score}";
    }

    public void ChangeHostHealth(int health)
    {
        hostHealth.text = $"호스트 체력 : {health}";
    }

    public void ChangeClientHealth(int health)
    {
        clientHealth.text = $"클라 체력 : {health}";
    }

    public void ChangeTimeLeft(float timeLeft)
    {
        TimeLeft.text = $"남은 시간 : {timeLeft}";
    }

}
