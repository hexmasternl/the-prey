package nl.hexmaster.theprey.ui.playfields.select

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.data.location.LocationSource
import nl.hexmaster.theprey.data.playfield.PlayFieldRepository
import nl.hexmaster.theprey.ui.playfields.ListState
import nl.hexmaster.theprey.ui.playfields.NearMeState
import nl.hexmaster.theprey.ui.playfields.NoLocationSource
import nl.hexmaster.theprey.ui.playfields.OwnPlayFields
import nl.hexmaster.theprey.ui.playfields.PlayFieldItem
import nl.hexmaster.theprey.ui.playfields.PlayFieldsTab
import nl.hexmaster.theprey.ui.playfields.PublicPlayFieldSearch
import nl.hexmaster.theprey.ui.playfields.SearchState
import nl.hexmaster.theprey.ui.playfields.isOn
import nl.hexmaster.theprey.ui.playfields.tab

data class PlayFieldSelectUiState(
    val selectedTab: PlayFieldsTab = PlayFieldsTab.Private,
    val mine: ListState = ListState.Loading,
    val query: String = "",
    val search: SearchState = SearchState.Idle,
    val nearMe: NearMeState = NearMeState.Off,
    /** Kept by value, so it survives tab switches and searches that no longer list it. */
    val selected: PlayFieldSelection? = null,
)

/**
 * The playfield selection screen: the own playfields and the public search, read-only, with one
 * selection spanning both tabs. Tab, search text, Near me and selection survive a configuration change
 * through [savedStateHandle]. Unlike the playfields list there is no refresh on resume: the player
 * cannot change their playfields from inside the picker.
 */
class PlayFieldSelectViewModel(
    repository: PlayFieldRepository,
    private val savedStateHandle: SavedStateHandle,
    locationSource: LocationSource = NoLocationSource,
    hasLocationAccess: () -> Boolean = { false },
) : ViewModel() {

    private val mine = OwnPlayFields(repository, viewModelScope)
    private val publicSearch = PublicPlayFieldSearch(
        repository,
        viewModelScope,
        initialQuery = savedStateHandle.get<String>(KEY_QUERY).orEmpty(),
        locationSource = locationSource,
        hasLocationAccess = hasLocationAccess,
        initialNearMe = savedStateHandle.get<Boolean>(KEY_NEAR_ME) ?: false,
    )

    private val _uiState = MutableStateFlow(
        PlayFieldSelectUiState(
            selectedTab = savedStateHandle.tab(KEY_TAB),
            query = publicSearch.query.value,
            selected = restoredSelection(),
        ),
    )
    val uiState: StateFlow<PlayFieldSelectUiState> = _uiState.asStateFlow()

    init {
        viewModelScope.launch { mine.state.collect { list -> _uiState.update { it.copy(mine = list) } } }
        viewModelScope.launch { publicSearch.query.collect { text -> _uiState.update { it.copy(query = text) } } }
        viewModelScope.launch { publicSearch.state.collect { search -> _uiState.update { it.copy(search = search) } } }
        viewModelScope.launch { publicSearch.nearMe.collect { nearMe -> _uiState.update { it.copy(nearMe = nearMe) } } }
        mine.refresh()
    }

    fun selectTab(tab: PlayFieldsTab) {
        savedStateHandle[KEY_TAB] = tab.name
        _uiState.update { it.copy(selectedTab = tab) }
    }

    /** Downloads the own playfields again after a failure. */
    fun retry() = mine.refresh()

    fun onQueryChange(text: String) {
        savedStateHandle[KEY_QUERY] = text
        publicSearch.onQueryChange(text)
    }

    /** Switches Near me on or off; see [PublicPlayFieldSearch.toggleNearMe]. */
    fun toggleNearMe() {
        publicSearch.toggleNearMe()
        savedStateHandle[KEY_NEAR_ME] = publicSearch.nearMe.value.isOn
    }

    /** Selects [item], moving the selection from any other row; tapping the selected row deselects it. */
    fun toggle(item: PlayFieldItem) {
        val selection = if (_uiState.value.selected?.id == item.id) {
            null
        } else {
            PlayFieldSelection(item.id, item.name, item.isPublic)
        }
        savedStateHandle[KEY_SELECTED_ID] = selection?.id
        savedStateHandle[KEY_SELECTED_NAME] = selection?.name
        savedStateHandle[KEY_SELECTED_PUBLIC] = selection?.isPublic
        _uiState.update { it.copy(selected = selection) }
    }

    private fun restoredSelection(): PlayFieldSelection? {
        val id = savedStateHandle.get<String>(KEY_SELECTED_ID) ?: return null
        return PlayFieldSelection(
            id = id,
            name = savedStateHandle.get<String>(KEY_SELECTED_NAME).orEmpty(),
            isPublic = savedStateHandle.get<Boolean>(KEY_SELECTED_PUBLIC) ?: false,
        )
    }

    internal companion object {
        const val KEY_TAB = "playfield_select_tab"
        const val KEY_QUERY = "playfield_select_query"
        const val KEY_NEAR_ME = "playfield_select_near_me"
        const val KEY_SELECTED_ID = "playfield_select_id"
        const val KEY_SELECTED_NAME = "playfield_select_name"
        const val KEY_SELECTED_PUBLIC = "playfield_select_public"
    }
}
