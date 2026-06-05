package nl.hexmaster.theprey;

import com.getcapacitor.BridgeActivity;

public class MainActivity extends BridgeActivity {

    @Override
    public void onCreate(android.os.Bundle savedInstanceState) {
        registerPlugin(GameLocationPlugin.class);
        super.onCreate(savedInstanceState);
    }
}
