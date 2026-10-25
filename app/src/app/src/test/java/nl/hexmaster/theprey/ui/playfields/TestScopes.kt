package nl.hexmaster.theprey.ui.playfields

import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.UnconfinedTestDispatcher

/** [this] scope running eagerly on [test]'s scheduler, so state holders update before the next line. */
@OptIn(ExperimentalCoroutinesApi::class)
fun CoroutineScope.plusUnconfined(test: TestScope): CoroutineScope =
    CoroutineScope(coroutineContext + UnconfinedTestDispatcher(test.testScheduler))
