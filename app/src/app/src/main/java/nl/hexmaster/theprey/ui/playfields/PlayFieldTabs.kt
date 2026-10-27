package nl.hexmaster.theprey.ui.playfields

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FilterChipDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.PrimaryTabRow
import androidx.compose.material3.Tab
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.unit.dp
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.ui.theme.ThePreyTheme

// Building blocks shared by the playfields list and the playfield selection screen.

/** The Private / Public tab row. */
@Composable
internal fun PlayFieldTabRow(selectedTab: PlayFieldsTab, onSelectTab: (PlayFieldsTab) -> Unit) {
    PrimaryTabRow(
        selectedTabIndex = selectedTab.ordinal,
        containerColor = MaterialTheme.colorScheme.background,
        contentColor = MaterialTheme.colorScheme.primary,
        divider = { RowSeparator() },
    ) {
        PlayFieldsTab.entries.forEach { tab ->
            Tab(
                selected = selectedTab == tab,
                onClick = { onSelectTab(tab) },
                selectedContentColor = MaterialTheme.colorScheme.primary,
                unselectedContentColor = MaterialTheme.colorScheme.onSurfaceVariant,
                text = {
                    Text(
                        text = stringResource(
                            when (tab) {
                                PlayFieldsTab.Private -> R.string.playfields_tab_private
                                PlayFieldsTab.Public -> R.string.playfields_tab_public
                            },
                        ).uppercase(),
                        style = MaterialTheme.typography.labelLarge,
                    )
                },
            )
        }
    }
}

/**
 * The Public tab's search field. The tab enters composition when it is selected, so the field
 * takes focus and opens the keyboard then; the keyboard's search action only closes it, because
 * the search is already live.
 */
@Composable
internal fun PlayFieldSearchField(query: String, onQueryChange: (String) -> Unit) {
    val focusRequester = remember { FocusRequester() }
    val keyboard = LocalSoftwareKeyboardController.current
    LaunchedEffect(Unit) {
        focusRequester.requestFocus()
        keyboard?.show()
    }
    OutlinedTextField(
        value = query,
        onValueChange = onQueryChange,
        label = { Text(stringResource(R.string.playfields_search_label)) },
        leadingIcon = { Icon(painter = painterResource(R.drawable.ic_search), contentDescription = null) },
        singleLine = true,
        keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
        keyboardActions = KeyboardActions(onSearch = { keyboard?.hide() }),
        shape = MaterialTheme.shapes.small,
        colors = OutlinedTextFieldDefaults.colors(
            focusedBorderColor = MaterialTheme.colorScheme.primary,
            unfocusedBorderColor = MaterialTheme.colorScheme.outline,
            focusedLabelColor = MaterialTheme.colorScheme.primary,
            unfocusedLabelColor = MaterialTheme.colorScheme.onSurfaceVariant,
            cursorColor = MaterialTheme.colorScheme.primary,
            focusedLeadingIconColor = MaterialTheme.colorScheme.primary,
            unfocusedLeadingIconColor = MaterialTheme.colorScheme.onSurfaceVariant,
        ),
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 12.dp)
            .focusRequester(focusRequester),
    )
}

/**
 * The own list's loading, error-with-retry and [emptyText] states; [content] draws a non-empty list.
 */
@Composable
internal fun OwnPlayFieldsState(
    mine: ListState,
    emptyText: String,
    onRetry: () -> Unit,
    content: @Composable (List<PlayFieldItem>) -> Unit,
) {
    when (mine) {
        ListState.Loading -> MessageState(text = null, loading = true)
        ListState.Error -> MessageState(
            text = stringResource(R.string.playfields_load_error),
            actionLabel = stringResource(R.string.playfields_retry),
            onAction = onRetry,
        )
        is ListState.Loaded -> if (mine.items.isEmpty()) MessageState(text = emptyText) else content(mine.items)
    }
}

/**
 * The Near me filter chip below the search field, with a line that says why it is off (no
 * permission, no fix) or that it is still locating. It never asks for a permission.
 */
@Composable
internal fun NearMeFilter(nearMe: NearMeState, onToggle: () -> Unit) {
    Column(Modifier.fillMaxWidth().padding(start = 16.dp, end = 16.dp, bottom = 4.dp)) {
        FilterChip(
            selected = nearMe.isOn,
            onClick = onToggle,
            label = { Text(stringResource(R.string.playfields_near_me)) },
            leadingIcon = {
                Icon(
                    painter = painterResource(R.drawable.ic_target),
                    contentDescription = null,
                    modifier = Modifier.size(FilterChipDefaults.IconSize),
                )
            },
            colors = FilterChipDefaults.filterChipColors(
                selectedContainerColor = MaterialTheme.colorScheme.primary.copy(alpha = 0.16f),
                selectedLabelColor = MaterialTheme.colorScheme.primary,
                selectedLeadingIconColor = MaterialTheme.colorScheme.primary,
                labelColor = MaterialTheme.colorScheme.onSurfaceVariant,
                iconColor = MaterialTheme.colorScheme.onSurfaceVariant,
            ),
        )
        val message = when (nearMe) {
            NearMeState.Locating -> R.string.playfields_near_me_locating
            NearMeState.NoPermission -> R.string.playfields_near_me_no_permission
            NearMeState.NoFix -> R.string.playfields_near_me_no_fix
            NearMeState.Off, is NearMeState.On -> null
        }
        if (message != null) {
            Text(
                text = stringResource(message),
                style = MaterialTheme.typography.bodySmall,
                color = if (nearMe == NearMeState.Locating) {
                    MaterialTheme.colorScheme.onSurfaceVariant
                } else {
                    ThePreyTheme.colors.caution
                },
            )
        }
    }
}

/**
 * The public search's hint, searching, error and no-results states; [content] draws the results.
 * With [nearMe] on, an empty result says nothing was found nearby.
 */
@Composable
internal fun PublicSearchState(
    search: SearchState,
    nearMe: NearMeState = NearMeState.Off,
    content: @Composable (List<PlayFieldItem>) -> Unit,
) {
    when (search) {
        SearchState.Idle -> MessageState(text = stringResource(R.string.playfields_search_hint))
        SearchState.Searching -> MessageState(text = null, loading = true)
        SearchState.Error -> MessageState(text = stringResource(R.string.playfields_search_error))
        is SearchState.Results -> if (search.items.isEmpty()) {
            MessageState(
                text = stringResource(
                    if (nearMe is NearMeState.On) R.string.playfields_near_me_none else R.string.playfields_no_results,
                ),
            )
        } else {
            content(search.items)
        }
    }
}
