using UnityEngine;

namespace CardClash
{
    public class NetworkConnectionChecker : MonoBehaviour
    {
        #region Scripts References

        [SerializeField] private UIManager _uiManager;
        [SerializeField] private ConnectionErrorManager _connectionErrorManager;

        #endregion

        #region Unity Lifecycle

        public bool HasNetworkConnection() => Application.internetReachability != NetworkReachability.NotReachable;

        public bool CheckInitialConnection()
        {
            if (HasNetworkConnection()) return true;

            _uiManager.SetState(ScreenType.ConnectionError, true);
            _connectionErrorManager.ShowError(ConnectionErrorType.NoInternet);

            return false;
        }

        #endregion
    }
}
