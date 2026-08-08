namespace MetamaskSetup.Playwright.MetaMask.Pages.Selectors;

public static class SettingsPageSelectors
{
    public static string PopularNetworksTabButton =>
        ".network-manager__tab-list .tab:nth-of-type(1) button";

    public static string AdditionalNetworksSection =>
        "[data-testid='additional-network-item'], .network-manager__tab-content";

    public static string NetworksItem(string networkName) => SelectorCreator.DataTestIdSelector(networkName);

    public static string AdditionalNetworkItem => SelectorCreator.DataTestIdSelector("additional-network-item");
    public static string ConfirmAddingNewNetwork => SelectorCreator.DataTestIdSelector("confirmation-submit-button");

    public static string CustomNetworksTabButton =>
        ".network-manager__tab-list .tab:nth-of-type(2) button";
        
    public static string AddCustomNetworkButton => ".network-manager__tab-content button";
    public static class Networks
    {
        public static string CloseModalButton => SelectorCreator.DataTestIdSelector("modal-header-close-button");
        public static class NewNetworkForm
        {
            private const string NewNetworkFormContainer = ".mm-modal-content__dialog";

            public static string NetworkNameInput => SelectorCreator.DataTestIdSelector("network-form-network-name");
            public static string AddRpcDropdown => SelectorCreator.DataTestIdSelector("test-add-rpc-drop-down");
            public static string BlockExplorerDropdown => SelectorCreator.DataTestIdSelector("test-explorer-drop-down");
            public static string AddBlockExplorerButton => ".dropdown-editor__item";
            public static string BlockExplorerUrlInput => SelectorCreator.DataTestIdSelector("explorer-url-input");

            public static string AddRpcButton => ".dropdown-editor__item-popover";

            public static string RpcUrlInput => SelectorCreator.DataTestIdSelector("rpc-url-input-test");
            
            public static string RpcNameInput => SelectorCreator.DataTestIdSelector("rpc-name-input-test");
            public static string AddRpcUrlButton => ".add-rpc-modal__footer button";
            public static string AddBlockExplorerUrlButton => ".add-block-explorer-modal__footer";
            
            public static string RpcUrlError => $"{NewNetworkFormContainer} .mm-box--color-error-default";
            public static string ChainIdInput => SelectorCreator.DataTestIdSelector("network-form-chain-id");
           
            public static string SymbolInput => SelectorCreator.DataTestIdSelector("network-form-ticker-input");
            public static string SaveButton =>  ".networks-tab__network-form__footer .mm-button-base";
        }
    }
}